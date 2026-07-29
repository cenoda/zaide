using System;
using Xunit;
using Zaide.Features.Agents.Application.Transparency.Trace;
using Zaide.Features.Agents.Domain.Transparency.Trace;

namespace Zaide.Tests.Features.Agents.Transparency.Trace;

/// <summary>
/// Phase 21 M2 trace redaction behavior. Every capture must redact before
/// any durable write, render, export, log, index, backup, or cross-process
/// transfer. Redaction failure is fail-closed: the original payload is
/// never admitted; a bounded failure marker is the only retained value.
/// </summary>
public sealed class Phase21RedactionTests
{
    [Fact]
    public void Apply_PassesThroughSafePayload()
    {
        var outcome = AgentTraceRedactionProcessor.Apply(
            "{\"method\":\"initialize\",\"direction\":\"in\"}");

        Assert.False(outcome.DidProcessingFail);
        Assert.Equal("{\"method\":\"initialize\",\"direction\":\"in\"}", outcome.Content);
    }

    [Fact]
    public void Apply_RedactsOpenAiApiKey()
    {
        var outcome = AgentTraceRedactionProcessor.Apply(
            "Authorization: Bearer sk-abcdefghijklmnopqrstuvwxyz0123456789");

        Assert.False(outcome.DidProcessingFail);
        Assert.Contains("[REDACTED:api-key]", outcome.Content);
        Assert.DoesNotContain("sk-abcdefghijklmnopqrstuvwxyz", outcome.Content);
    }

    [Fact]
    public void Apply_RedactsAwsAccessKey()
    {
        var outcome = AgentTraceRedactionProcessor.Apply(
            "aws: AKIAABCDEFGHIJKLMNOP");

        Assert.False(outcome.DidProcessingFail);
        Assert.Contains("[REDACTED:api-key]", outcome.Content);
        Assert.DoesNotContain("AKIAABCDEFGHIJKLMNOP", outcome.Content);
    }

    [Fact]
    public void Apply_RedactsPEMPrivateKey()
    {
        var outcome = AgentTraceRedactionProcessor.Apply(
            "-----BEGIN RSA PRIVATE KEY-----\nABCDEFG\n-----END RSA PRIVATE KEY-----");

        Assert.False(outcome.DidProcessingFail);
        Assert.DoesNotContain("ABCDEFG", outcome.Content);
        Assert.Contains("[REDACTED:private-key]", outcome.Content);
    }

    [Fact]
    public void Apply_RedactsConnectionStringPassword()
    {
        var outcome = AgentTraceRedactionProcessor.Apply(
            "Server=db.local;Password=hunter2;Database=app");

        Assert.False(outcome.DidProcessingFail);
        Assert.DoesNotContain("hunter2", outcome.Content);
        Assert.Contains("[REDACTED:api-key]", outcome.Content);
    }

    [Fact]
    public void Apply_RedactsHexSecretLabel()
    {
        var outcome = AgentTraceRedactionProcessor.Apply(
            "token=0123456789abcdef0123456789abcdef");

        Assert.False(outcome.DidProcessingFail);
        Assert.Contains("[REDACTED:hex-secret]", outcome.Content);
    }

    [Fact]
    public void Apply_IsFailClosedOnNullPayload()
    {
        var outcome = AgentTraceRedactionProcessor.Apply(payload: null!);

        Assert.True(outcome.DidProcessingFail);
        Assert.Equal(AgentTraceCaptureState.Failed, outcome.State);
    }

    [Fact]
    public void Apply_StripsUtf8BomBeforeScanning()
    {
        var outcome = AgentTraceRedactionProcessor.Apply(
            "﻿{\"method\":\"initialize\"}");

        Assert.False(outcome.DidProcessingFail);
        Assert.Equal("{\"method\":\"initialize\"}", outcome.Content);
    }

    [Fact]
    public void Apply_ReachesRedactedStateWhenAnyPatternMatches()
    {
        var outcome = AgentTraceRedactionProcessor.Apply(
            "Authorization: Bearer sk-abcdefghijklmnopqrstuvwxyz0123456789");

        Assert.False(outcome.DidProcessingFail);
        Assert.Equal(AgentTraceCaptureState.Redacted, outcome.State);
        Assert.NotNull(outcome.Reason);
        Assert.Equal("api-key", outcome.Reason!.SecretClass);
    }

    [Fact]
    public void Apply_PreservesByteCountForSizeEnforcement()
    {
        var payload = "sk-abcdefghijklmnopqrstuvwxyz0123456789 in Authorization header";
        var outcome = AgentTraceRedactionProcessor.Apply(payload);

        Assert.False(outcome.DidProcessingFail);
        Assert.True(outcome.ByteCount > 0);
    }
}
