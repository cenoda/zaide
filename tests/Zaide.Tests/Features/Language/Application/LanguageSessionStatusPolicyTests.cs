using System;
using Xunit;
using Zaide.Services;
using Zaide.Features.Language.Contracts;
using Zaide.Features.Language.Application;

namespace Zaide.Tests.Features.Language.Application;

public sealed class LanguageSessionStatusPolicyTests
{
    [Theory]
    [InlineData(LanguageSessionState.Ready, "C# · Ready")]
    [InlineData(LanguageSessionState.Loading, "C# · Loading…")]
    [InlineData(LanguageSessionState.Failed, "C# · Failed")]
    [InlineData(LanguageSessionState.Cancelled, "C# · Cancelled")]
    public void MapStatusBarText_ProjectsSessionStates(LanguageSessionState state, string expected)
    {
        var snapshot = new LanguageSessionSnapshot(state, 1, "/p.csproj", "/p", null, null);
        Assert.Equal(expected, LanguageSessionStatusPolicy.MapStatusBarText(snapshot));
    }

    [Fact]
    public void MapStatusBarText_UnavailableWithoutProject_IsEmpty()
    {
        var snapshot = new LanguageSessionSnapshot(
            LanguageSessionState.Unavailable, 0, null, null, null, null);
        Assert.Equal(string.Empty, LanguageSessionStatusPolicy.MapStatusBarText(snapshot));
    }

    [Fact]
    public void MapFailureMessage_DoesNotExposeRawProtocolText()
    {
        var failure = new LanguageSessionFailure(
            LanguageSessionFailureKind.InitializeFailed,
            "RemoteInvocationException: Internal error");

        Assert.Equal(
            "C# language server failed to start.",
            LanguageSessionStatusPolicy.MapFailureMessage(failure));
    }

    [Fact]
    public void MapProblemsStatusMessage_Failed_UsesPolicyMessage()
    {
        var snapshot = new LanguageDiagnosticsSnapshot(
            LanguageSessionState.Failed,
            1,
            new LanguageSessionFailure(LanguageSessionFailureKind.ServerExited, "raw"),
            Array.Empty<LanguageDiagnostic>());

        Assert.Equal(
            "C# language server exited unexpectedly.",
            LanguageSessionStatusPolicy.MapProblemsStatusMessage(snapshot));
    }
}