using System;
using System.Collections.Generic;
using System.Text.Json;
using Zaide.Features.Agents.Domain;

namespace Zaide.Features.Agents.Application;

/// <summary>
/// Validates OpenAI tool-call arguments and maps them to Phase 17 action payloads.
/// </summary>
internal static class NativeHarnessToolArgumentMapper
{
    public static bool TryCreateDescriptor(
        NativeHarnessToolCallId toolCallId,
        string modelToolName,
        string argumentsJson,
        out NativeHarnessToolCallDescriptor? descriptor,
        out string error)
    {
        descriptor = null;
        error = string.Empty;

        if (!TryMapToolName(modelToolName, out var actionKind))
        {
            error = $"Unsupported tool name '{modelToolName}'.";
            return false;
        }

        if (!NativeHarnessToolCallDescriptor.IsSupportedActionKind(actionKind))
        {
            error = $"Action kind '{actionKind}' is not supported.";
            return false;
        }

        try
        {
            descriptor = new NativeHarnessToolCallDescriptor(
                toolCallId,
                actionKind,
                modelToolName,
                argumentsJson,
                correlationKey: toolCallId.Value);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
        {
            error = ex.Message;
            return false;
        }
    }

    public static bool TryMapToPayload(
        NativeHarnessToolCallDescriptor descriptor,
        out AgentActionPayload? payload,
        out string error)
    {
        payload = null;
        error = string.Empty;

        try
        {
            using var document = JsonDocument.Parse(descriptor.ArgumentsJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "Tool arguments must be a JSON object.";
                return false;
            }

            payload = descriptor.ActionKind switch
            {
                AgentActionKind.ReadFile => MapReadFile(root),
                AgentActionKind.CreateFile => MapCreateFile(root),
                AgentActionKind.ReplaceFile => MapReplaceFile(root),
                AgentActionKind.DeleteFile => MapDeleteFile(root),
                AgentActionKind.ExecuteCommand => MapExecuteCommand(root),
                _ => throw new InvalidOperationException("Unsupported action kind."),
            };

            return true;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or ArgumentOutOfRangeException)
        {
            error = ex.Message;
            payload = null;
            return false;
        }
    }

    private static bool TryMapToolName(string modelToolName, out AgentActionKind actionKind)
    {
        switch (modelToolName)
        {
            case NativeHarnessProviderProtocol.ReadFileToolName:
                actionKind = AgentActionKind.ReadFile;
                return true;
            case NativeHarnessProviderProtocol.CreateFileToolName:
                actionKind = AgentActionKind.CreateFile;
                return true;
            case NativeHarnessProviderProtocol.ReplaceFileToolName:
                actionKind = AgentActionKind.ReplaceFile;
                return true;
            case NativeHarnessProviderProtocol.DeleteFileToolName:
                actionKind = AgentActionKind.DeleteFile;
                return true;
            case NativeHarnessProviderProtocol.ExecuteCommandToolName:
                actionKind = AgentActionKind.ExecuteCommand;
                return true;
            default:
                actionKind = default;
                return false;
        }
    }

    private static AgentReadFileActionPayload MapReadFile(JsonElement root)
    {
        var path = RequireString(root, "path");
        return new AgentReadFileActionPayload(AgentWorkspaceRelativePath.Normalize(path));
    }

    private static AgentCreateFileActionPayload MapCreateFile(JsonElement root)
    {
        var path = RequireString(root, "path");
        var content = RequireString(root, "content", allowEmpty: true);
        return new AgentCreateFileActionPayload(
            AgentWorkspaceRelativePath.Normalize(path),
            content);
    }

    private static AgentReplaceFileActionPayload MapReplaceFile(JsonElement root)
    {
        var path = RequireString(root, "path");
        var baseRevision = RequireString(root, "base_revision");
        var content = RequireString(root, "content", allowEmpty: true);
        return new AgentReplaceFileActionPayload(
            AgentWorkspaceRelativePath.Normalize(path),
            AgentContentRevision.FromDigest(baseRevision),
            content);
    }

    private static AgentDeleteFileActionPayload MapDeleteFile(JsonElement root)
    {
        var path = RequireString(root, "path");
        var baseRevision = RequireString(root, "base_revision");
        return new AgentDeleteFileActionPayload(
            AgentWorkspaceRelativePath.Normalize(path),
            AgentContentRevision.FromDigest(baseRevision));
    }

    private static AgentExecuteCommandActionPayload MapExecuteCommand(JsonElement root)
    {
        var executable = RequireString(root, "executable");
        var workingDirectory = RequireString(root, "working_directory");
        var arguments = ReadStringArray(root, "arguments");
        return new AgentExecuteCommandActionPayload(
            executable,
            arguments,
            AgentWorkspaceRelativePath.Normalize(workingDirectory));
    }

    private static string RequireString(JsonElement root, string propertyName, bool allowEmpty = false)
    {
        if (!root.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            throw new ArgumentException($"Missing or invalid '{propertyName}' argument.");
        }

        var value = property.GetString() ?? string.Empty;
        if (!allowEmpty && string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"'{propertyName}' must not be empty.");
        }

        return value;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return Array.Empty<string>();
        }

        if (property.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException($"'{propertyName}' must be a JSON array.");
        }

        var values = new List<string>();
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException($"'{propertyName}' must contain only strings.");
            }

            var value = item.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"'{propertyName}' cannot contain blank values.");
            }

            values.Add(value);
        }

        return values;
    }
}
