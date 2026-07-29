using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Zaide.Features.Agents.Contracts.Transparency.Memory;
using Zaide.Features.Agents.Domain.Transparency.Memory;

namespace Zaide.Features.Agents.Application.Memory;

internal sealed class AgentMemoryPolicyEvaluator : IAgentMemoryPolicyEvaluator
{
    private static readonly Regex PoisoningPattern = new(
        @"(ignore\s+(all\s+)?previous\s+instructions|system\s+prompt\s+override|exfiltrat(e|ion)|delete\s+all\s+files)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public AgentMemoryPolicyEvaluation EvaluateCreate(
        AgentMemoryCreateRequest request,
        IReadOnlyList<AgentMemoryRecord> existingRecords)
    {
        if (request.Content.Length > AgentMemoryLimits.MaxContentLength)
        {
            return new AgentMemoryPolicyEvaluation(
                AgentMemoryConflictKind.ScopeConflict,
                isPoisoningSuspect: false,
                isStaleFact: false,
                reason: "Content exceeds maximum length.");
        }

        var poisoning = DetectPoisoning(request.Content, request.Provenance.SourceKind);
        var stale = DetectStale(request.LastValidatedAtUtc);
        var conflict = DetectContentConflict(request.ScopeTarget, request.Content, existingRecords);

        if (poisoning)
        {
            return new AgentMemoryPolicyEvaluation(
                AgentMemoryConflictKind.PoisoningSuspect,
                isPoisoningSuspect: true,
                isStaleFact: stale,
                reason: "Content matches poisoning suspect pattern.");
        }

        if (conflict != AgentMemoryConflictKind.None)
        {
            return new AgentMemoryPolicyEvaluation(
                conflict,
                isPoisoningSuspect: false,
                isStaleFact: stale,
                reason: "Conflicting active memory exists for scope.");
        }

        return new AgentMemoryPolicyEvaluation(
            AgentMemoryConflictKind.None,
            isPoisoningSuspect: false,
            isStaleFact: stale);
    }

    public AgentMemoryPolicyEvaluation EvaluateCorrect(
        AgentMemoryCorrectRequest request,
        AgentMemoryRecord existing)
    {
        if (existing.Status is AgentMemoryStatus.Deleted or AgentMemoryStatus.Superseded)
        {
            return new AgentMemoryPolicyEvaluation(
                AgentMemoryConflictKind.ScopeConflict,
                isPoisoningSuspect: false,
                isStaleFact: false,
                reason: "Cannot correct deleted or superseded memory.");
        }

        var poisoning = DetectPoisoning(request.Content, request.Provenance.SourceKind);
        var stale = DetectStale(request.LastValidatedAtUtc ?? existing.LastValidatedAtUtc);

        if (poisoning)
        {
            return new AgentMemoryPolicyEvaluation(
                AgentMemoryConflictKind.PoisoningSuspect,
                isPoisoningSuspect: true,
                isStaleFact: stale,
                reason: "Corrected content matches poisoning suspect pattern.");
        }

        return new AgentMemoryPolicyEvaluation(
            AgentMemoryConflictKind.None,
            isPoisoningSuspect: false,
            isStaleFact: stale);
    }

    public AgentMemoryPolicyEvaluation EvaluateSupersede(
        AgentMemorySupersedeRequest request,
        AgentMemoryRecord superseded)
    {
        if (superseded.Status is AgentMemoryStatus.Deleted)
        {
            return new AgentMemoryPolicyEvaluation(
                AgentMemoryConflictKind.ScopeConflict,
                isPoisoningSuspect: false,
                isStaleFact: false,
                reason: "Cannot supersede deleted memory.");
        }

        if (superseded.ScopeTarget.Scope != request.ScopeTarget.Scope)
        {
            return new AgentMemoryPolicyEvaluation(
                AgentMemoryConflictKind.ScopeConflict,
                isPoisoningSuspect: false,
                isStaleFact: false,
                reason: "Supersession scope mismatch.");
        }

        var poisoning = DetectPoisoning(request.Content, request.Provenance.SourceKind);
        var stale = DetectStale(request.LastValidatedAtUtc);

        return new AgentMemoryPolicyEvaluation(
            poisoning ? AgentMemoryConflictKind.PoisoningSuspect : AgentMemoryConflictKind.None,
            isPoisoningSuspect: poisoning,
            isStaleFact: stale);
    }

    private static bool DetectPoisoning(string content, AgentMemorySourceKind sourceKind)
    {
        if (sourceKind == AgentMemorySourceKind.Import)
        {
            return true;
        }

        return PoisoningPattern.IsMatch(content);
    }

    private static bool DetectStale(DateTimeOffset? lastValidatedAtUtc)
    {
        if (lastValidatedAtUtc is null)
        {
            return false;
        }

        var threshold = DateTimeOffset.UtcNow.AddDays(-AgentMemoryLimits.DefaultStaleValidationDays);
        return lastValidatedAtUtc < threshold;
    }

    private static AgentMemoryConflictKind DetectContentConflict(
        AgentMemoryScopeTarget scopeTarget,
        string content,
        IReadOnlyList<AgentMemoryRecord> existingRecords)
    {
        var contentHash = ComputeContentHash(content);
        foreach (var existing in existingRecords)
        {
            if (!existing.IsRetrievable)
            {
                continue;
            }

            if (!ScopeMatches(scopeTarget, existing.ScopeTarget))
            {
                continue;
            }

            var existingHash = ComputeContentHash(existing.Content);
            if (!string.Equals(contentHash, existingHash, StringComparison.Ordinal))
            {
                return AgentMemoryConflictKind.ContentConflict;
            }
        }

        return AgentMemoryConflictKind.None;
    }

    private static bool ScopeMatches(AgentMemoryScopeTarget left, AgentMemoryScopeTarget right)
    {
        if (left.Scope != right.Scope)
        {
            return false;
        }

        return left.Scope switch
        {
            AgentMemoryScope.Session => left.SessionId == right.SessionId,
            AgentMemoryScope.Agent => left.ActorId == right.ActorId,
            AgentMemoryScope.Conversation => left.ConversationId == right.ConversationId,
            AgentMemoryScope.ProjectShared => string.Equals(
                left.ProjectId,
                right.ProjectId,
                StringComparison.Ordinal),
            _ => false,
        };
    }

    private static string ComputeContentHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content.Trim()));
        return Convert.ToHexString(bytes);
    }
}
