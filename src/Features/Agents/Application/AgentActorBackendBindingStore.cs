using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Zaide.Features.Agents.Contracts;
using Zaide.Features.Agents.Domain;
using Zaide.Features.Agents.Infrastructure;
using Zaide.Features.Conversations.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Durable schema-v1 actor/backend binding store with atomic temp → LKG → replace
/// writes, revisioned mutations, busy rejection, and post-success change events.
/// </summary>
internal sealed class AgentActorBackendBindingStore : IAgentActorBackendBindingStore
{
    private readonly Dictionary<ActorId, AgentActorBackendBinding> _bindings = new();
    private readonly object _sync = new();
    private readonly string? _primaryPath;
    private readonly string? _tempPath;
    private readonly string? _lastKnownGoodPath;
    private readonly bool _persistToDisk;
    private readonly IAgentActorActiveRunQuery? _activeRunQuery;
    private readonly List<Action<AgentActorBackendBindingChangedEvent>> _changeHandlers = new();
    private AgentActorBackendBindingLoadResult _loadResult =
        AgentActorBackendBindingLoadResult.Empty();

    /// <summary>
    /// In-memory store for tests and harnesses that construct the store directly.
    /// Production DI uses the durable path constructor.
    /// </summary>
    public AgentActorBackendBindingStore()
        : this(activeRunQuery: null)
    {
    }

    /// <summary>
    /// In-memory store with an optional active-run gate (no disk I/O).
    /// </summary>
    public AgentActorBackendBindingStore(IAgentActorActiveRunQuery? activeRunQuery)
    {
        _persistToDisk = false;
        _primaryPath = null;
        _tempPath = null;
        _lastKnownGoodPath = null;
        _activeRunQuery = activeRunQuery;
        _loadResult = AgentActorBackendBindingLoadResult.Empty();
    }

    /// <summary>Durable production/test constructor with explicit paths.</summary>
    internal AgentActorBackendBindingStore(
        string primaryPath,
        string tempPath,
        string lastKnownGoodPath,
        IAgentActorActiveRunQuery? activeRunQuery = null)
    {
        if (string.IsNullOrWhiteSpace(primaryPath))
        {
            throw new ArgumentException("Primary path is required.", nameof(primaryPath));
        }

        if (string.IsNullOrWhiteSpace(tempPath))
        {
            throw new ArgumentException("Temp path is required.", nameof(tempPath));
        }

        if (string.IsNullOrWhiteSpace(lastKnownGoodPath))
        {
            throw new ArgumentException("Last-known-good path is required.", nameof(lastKnownGoodPath));
        }

        _persistToDisk = true;
        _primaryPath = primaryPath;
        _tempPath = tempPath;
        _lastKnownGoodPath = lastKnownGoodPath;
        _activeRunQuery = activeRunQuery;
        LoadAtStartup();
    }

    public AgentActorBackendBindingLoadResult LoadResult
    {
        get
        {
            lock (_sync)
            {
                return _loadResult;
            }
        }
    }

    public event Action<AgentActorBackendBindingChangedEvent>? BindingChanged
    {
        add
        {
            if (value is null)
            {
                return;
            }

            lock (_sync)
            {
                _changeHandlers.Add(value);
            }
        }
        remove
        {
            if (value is null)
            {
                return;
            }

            lock (_sync)
            {
                _changeHandlers.Remove(value);
            }
        }
    }

    public bool TryGetBinding(ActorId actorId, out AgentActorBackendBinding binding)
    {
        lock (_sync)
        {
            return _bindings.TryGetValue(actorId, out binding!);
        }
    }

    public bool HasBinding(ActorId actorId)
    {
        lock (_sync)
        {
            return _bindings.ContainsKey(actorId);
        }
    }

    public AgentBackendId GetRequiredBackendId(ActorId actorId)
    {
        lock (_sync)
        {
            if (!_bindings.TryGetValue(actorId, out var binding))
            {
                throw new InvalidOperationException(
                    "No explicit backend binding exists for this actor.");
            }

            return binding.BackendId;
        }
    }

    public long GetRevision(ActorId actorId)
    {
        lock (_sync)
        {
            return _bindings.TryGetValue(actorId, out var binding) ? binding.Revision : 0;
        }
    }

    /// <summary>
    /// Compatibility path for existing production readers and tests that still
    /// call <c>SetBinding</c>. User/workflow mutations must use
    /// <see cref="TryBind"/> / <see cref="TryUpdate"/> / <see cref="TryUnbind"/>.
    /// This helper persists the supplied binding without revision/busy gates so
    /// Phase 19–21 harnesses remain functional; it still uses the durable path.
    /// </summary>
    public void SetBinding(AgentActorBackendBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);

        AgentActorBackendBindingChangedEvent? change = null;
        lock (_sync)
        {
            var previous = _bindings.TryGetValue(binding.ActorId, out var existing)
                ? existing
                : null;
            var candidate = binding.Revision < 1
                ? binding.WithRevision(1)
                : binding;

            // Preserve monotonic revision when callers supply an older revision.
            if (previous is not null && candidate.Revision <= previous.Revision)
            {
                candidate = candidate.WithRevision(previous.Revision + 1);
            }

            var nextBindings = CloneBindings();
            nextBindings[candidate.ActorId] = candidate;

            if (!TryPersistLocked(nextBindings, out _))
            {
                // Compatibility path: keep prior in-memory truth when disk fails.
                return;
            }

            _bindings[candidate.ActorId] = candidate;
            change = new AgentActorBackendBindingChangedEvent(
                candidate.ActorId,
                previous is null
                    ? AgentActorBackendBindingMutationKind.Bind
                    : AgentActorBackendBindingMutationKind.Update,
                candidate.Revision,
                isBound: true);
        }

        if (change is not null)
        {
            PublishChange(change);
        }
    }

    public void SetRuntimeAuthentication(
        ActorId actorId,
        string? selectedAuthMethodId,
        AgentAuthenticationConnectionState authenticationState)
    {
        lock (_sync)
        {
            if (!_bindings.TryGetValue(actorId, out var existing))
            {
                throw new InvalidOperationException(
                    "No explicit backend binding exists for this actor.");
            }

            // In-memory only: durable identity/revision unchanged.
            _bindings[actorId] = existing.WithAuthentication(
                selectedAuthMethodId,
                authenticationState);
        }
    }

    public AgentActorBackendBindingMutationResult TryBind(AgentActorBackendBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);

        AgentActorBackendBindingChangedEvent? change = null;
        AgentActorBackendBindingMutationResult result;
        lock (_sync)
        {
            try
            {
                // Bind always starts a durable revision series at 1 for a new actor
                // or advances past the existing revision when rebinding.
                var nextRevision = _bindings.TryGetValue(binding.ActorId, out var existing)
                    ? existing.Revision + 1
                    : 1;
                // Durable bind never treats auth/capability runtime state as durable truth.
                var candidate = binding
                    .WithClearedRuntimeAuth()
                    .WithRevision(nextRevision);

                var nextBindings = CloneBindings();
                nextBindings[candidate.ActorId] = candidate;

                if (!TryPersistLocked(nextBindings, out var persistenceError))
                {
                    result = AgentActorBackendBindingMutationResult.PersistenceFailed(
                        AgentActorBackendBindingMutationKind.Bind,
                        binding.ActorId,
                        existing?.Revision ?? 0,
                        persistenceError ?? "Failed to persist binding document.");
                    return result;
                }

                _bindings[candidate.ActorId] = candidate;
                change = new AgentActorBackendBindingChangedEvent(
                    candidate.ActorId,
                    AgentActorBackendBindingMutationKind.Bind,
                    candidate.Revision,
                    isBound: true);
                result = AgentActorBackendBindingMutationResult.Succeeded(
                    AgentActorBackendBindingMutationKind.Bind,
                    candidate.ActorId,
                    candidate.Revision,
                    candidate);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                var currentRevision = _bindings.TryGetValue(binding.ActorId, out var current)
                    ? current.Revision
                    : 0;
                result = AgentActorBackendBindingMutationResult.ValidationFailed(
                    AgentActorBackendBindingMutationKind.Bind,
                    binding.ActorId,
                    currentRevision,
                    ex.Message);
            }
        }

        if (change is not null)
        {
            PublishChange(change);
        }

        return result;
    }

    public AgentActorBackendBindingMutationResult TryUpdate(
        ActorId actorId,
        AgentActorBackendBinding binding,
        long expectedRevision)
    {
        ArgumentNullException.ThrowIfNull(binding);

        if (binding.ActorId != actorId)
        {
            return AgentActorBackendBindingMutationResult.ValidationFailed(
                AgentActorBackendBindingMutationKind.Update,
                actorId,
                GetRevision(actorId),
                "Update binding actor id must match the target actor.");
        }

        AgentActorBackendBindingChangedEvent? change = null;
        AgentActorBackendBindingMutationResult result;
        lock (_sync)
        {
            if (!_bindings.TryGetValue(actorId, out var existing))
            {
                result = AgentActorBackendBindingMutationResult.ValidationFailed(
                    AgentActorBackendBindingMutationKind.Update,
                    actorId,
                    currentRevision: 0,
                    "Cannot update an unbound actor; bind first.");
                return result;
            }

            if (existing.Revision != expectedRevision)
            {
                result = AgentActorBackendBindingMutationResult.Conflict(
                    AgentActorBackendBindingMutationKind.Update,
                    actorId,
                    existing.Revision,
                    "Binding revision conflict: expected revision is stale.");
                return result;
            }

            if (IsActorBusy(actorId))
            {
                result = AgentActorBackendBindingMutationResult.Busy(
                    AgentActorBackendBindingMutationKind.Update,
                    actorId,
                    existing.Revision,
                    "Cannot update binding while the actor has an active run.");
                return result;
            }

            try
            {
                // Idle update advances revision and clears runtime auth metadata.
                var candidate = binding
                    .WithClearedRuntimeAuth()
                    .WithRevision(existing.Revision + 1);

                var nextBindings = CloneBindings();
                nextBindings[actorId] = candidate;

                if (!TryPersistLocked(nextBindings, out var persistenceError))
                {
                    result = AgentActorBackendBindingMutationResult.PersistenceFailed(
                        AgentActorBackendBindingMutationKind.Update,
                        actorId,
                        existing.Revision,
                        persistenceError ?? "Failed to persist binding document.");
                    return result;
                }

                _bindings[actorId] = candidate;
                change = new AgentActorBackendBindingChangedEvent(
                    actorId,
                    AgentActorBackendBindingMutationKind.Update,
                    candidate.Revision,
                    isBound: true);
                result = AgentActorBackendBindingMutationResult.Succeeded(
                    AgentActorBackendBindingMutationKind.Update,
                    actorId,
                    candidate.Revision,
                    candidate);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                result = AgentActorBackendBindingMutationResult.ValidationFailed(
                    AgentActorBackendBindingMutationKind.Update,
                    actorId,
                    existing.Revision,
                    ex.Message);
            }
        }

        if (change is not null)
        {
            PublishChange(change);
        }

        return result;
    }

    public AgentActorBackendBindingMutationResult TryUnbind(ActorId actorId, long expectedRevision)
    {
        AgentActorBackendBindingChangedEvent? change = null;
        AgentActorBackendBindingMutationResult result;
        lock (_sync)
        {
            if (!_bindings.TryGetValue(actorId, out var existing))
            {
                result = AgentActorBackendBindingMutationResult.ValidationFailed(
                    AgentActorBackendBindingMutationKind.Unbind,
                    actorId,
                    currentRevision: 0,
                    "Actor is already unbound.");
                return result;
            }

            if (existing.Revision != expectedRevision)
            {
                result = AgentActorBackendBindingMutationResult.Conflict(
                    AgentActorBackendBindingMutationKind.Unbind,
                    actorId,
                    existing.Revision,
                    "Binding revision conflict: expected revision is stale.");
                return result;
            }

            if (IsActorBusy(actorId))
            {
                result = AgentActorBackendBindingMutationResult.Busy(
                    AgentActorBackendBindingMutationKind.Unbind,
                    actorId,
                    existing.Revision,
                    "Cannot unbind while the actor has an active run.");
                return result;
            }

            var nextBindings = CloneBindings();
            nextBindings.Remove(actorId);

            if (!TryPersistLocked(nextBindings, out var persistenceError))
            {
                result = AgentActorBackendBindingMutationResult.PersistenceFailed(
                    AgentActorBackendBindingMutationKind.Unbind,
                    actorId,
                    existing.Revision,
                    persistenceError ?? "Failed to persist binding document.");
                return result;
            }

            _bindings.Remove(actorId);
            change = new AgentActorBackendBindingChangedEvent(
                actorId,
                AgentActorBackendBindingMutationKind.Unbind,
                revision: 0,
                isBound: false);
            result = AgentActorBackendBindingMutationResult.Succeeded(
                AgentActorBackendBindingMutationKind.Unbind,
                actorId,
                revision: 0,
                binding: null);
        }

        if (change is not null)
        {
            PublishChange(change);
        }

        return result;
    }

    private bool IsActorBusy(ActorId actorId) =>
        _activeRunQuery?.HasActiveRun(actorId) == true;

    private Dictionary<ActorId, AgentActorBackendBinding> CloneBindings() =>
        _bindings.ToDictionary(pair => pair.Key, pair => pair.Value);

    private void LoadAtStartup()
    {
        if (!_persistToDisk
            || _primaryPath is null
            || _tempPath is null
            || _lastKnownGoodPath is null)
        {
            _loadResult = AgentActorBackendBindingLoadResult.Empty();
            return;
        }

        // Leftover temp is never loaded as current state.
        if (!File.Exists(_primaryPath))
        {
            if (TryLoadPath(_lastKnownGoodPath, out var lkgBindings, out var lkgUnsupported, out var lkgError)
                && lkgBindings is not null)
            {
                ReplaceInMemory(lkgBindings);
                _loadResult = AgentActorBackendBindingLoadResult.RecoveredFromLastKnownGood();
                return;
            }

            if (lkgUnsupported)
            {
                _loadResult = AgentActorBackendBindingLoadResult.UnsupportedSchema(
                    lkgError ?? "Unsupported binding document schema version.");
                return;
            }

            _loadResult = AgentActorBackendBindingLoadResult.Empty();
            return;
        }

        if (TryLoadPath(_primaryPath, out var primaryBindings, out var primaryUnsupported, out var primaryError)
            && primaryBindings is not null)
        {
            ReplaceInMemory(primaryBindings);
            _loadResult = AgentActorBackendBindingLoadResult.Loaded();
            return;
        }

        if (primaryUnsupported)
        {
            // Fail closed without rewriting the primary.
            _loadResult = AgentActorBackendBindingLoadResult.UnsupportedSchema(
                primaryError ?? "Unsupported binding document schema version.");
            return;
        }

        if (TryLoadPath(_lastKnownGoodPath, out var recovered, out var recoveredUnsupported, out var recoveredError)
            && recovered is not null)
        {
            ReplaceInMemory(recovered);
            _loadResult = AgentActorBackendBindingLoadResult.RecoveredFromLastKnownGood();
            return;
        }

        if (recoveredUnsupported)
        {
            _loadResult = AgentActorBackendBindingLoadResult.UnsupportedSchema(
                recoveredError ?? "Unsupported binding document schema version.");
            return;
        }

        _loadResult = AgentActorBackendBindingLoadResult.UnboundWithRecoveryError(
            primaryError
            ?? recoveredError
            ?? "Binding document is corrupt and no valid last-known-good copy is available.");
    }

    private bool TryLoadPath(
        string path,
        out IReadOnlyDictionary<ActorId, AgentActorBackendBinding>? bindings,
        out bool unsupportedSchema,
        out string? error)
    {
        bindings = null;
        unsupportedSchema = false;
        error = null;

        if (!File.Exists(path))
        {
            error = "Binding document is missing.";
            return false;
        }

        try
        {
            var json = File.ReadAllText(path);
            if (!AgentActorBackendBindingSerializer.TryDeserialize(
                    json,
                    out var document,
                    out unsupportedSchema,
                    out error)
                || document is null)
            {
                return false;
            }

            bindings = AgentActorBackendBindingSerializer.ToBindings(document);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            error = ex.Message;
            return false;
        }
    }

    private void ReplaceInMemory(IReadOnlyDictionary<ActorId, AgentActorBackendBinding> bindings)
    {
        _bindings.Clear();
        foreach (var pair in bindings)
        {
            // Durable rehydrate never restores authenticated/failed auth state.
            _bindings[pair.Key] = pair.Value.WithClearedRuntimeAuth();
        }
    }

    private bool TryPersistLocked(
        IReadOnlyDictionary<ActorId, AgentActorBackendBinding> nextBindings,
        out string? error)
    {
        error = null;

        // Always validate the candidate document, even in memory-only mode.
        try
        {
            var document = AgentActorBackendBindingSerializer.FromBindings(nextBindings);
            var json = AgentActorBackendBindingSerializer.Serialize(document);

            if (!_persistToDisk
                || _primaryPath is null
                || _tempPath is null
                || _lastKnownGoodPath is null)
            {
                return true;
            }

            var directory = Path.GetDirectoryName(_primaryPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 1. Validate entire candidate (done by Serialize/FromBindings)
            // 2. Write same-directory temp
            WriteTempFile(json);

            // 3. Prepare LKG backup of current primary (when primary exists)
            if (File.Exists(_primaryPath))
            {
                File.Copy(_primaryPath, _lastKnownGoodPath, overwrite: true);
            }

            // 4. Atomic replace primary
            File.Move(_tempPath, _primaryPath, overwrite: true);
            return true;
        }
        catch (Exception ex) when (
            ex is IOException
                or UnauthorizedAccessException
                or InvalidOperationException
                or ArgumentException
                or JsonException)
        {
            error = ex.Message;
            TryDeleteTempQuietly();
            return false;
        }
    }

    private void WriteTempFile(string json)
    {
        if (_tempPath is null)
        {
            return;
        }

        if (File.Exists(_tempPath))
        {
            File.Delete(_tempPath);
        }

        File.WriteAllText(_tempPath, json);
    }

    private void TryDeleteTempQuietly()
    {
        if (_tempPath is null)
        {
            return;
        }

        try
        {
            if (File.Exists(_tempPath))
            {
                File.Delete(_tempPath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Leftover temp is never loaded as current; ignore cleanup failure.
        }
    }

    private void PublishChange(AgentActorBackendBindingChangedEvent change)
    {
        Action<AgentActorBackendBindingChangedEvent>[] handlers;
        lock (_sync)
        {
            handlers = _changeHandlers.ToArray();
        }

        foreach (var handler in handlers)
        {
            handler(change);
        }
    }
}
