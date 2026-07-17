using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Models;
using Zaide.Services;
using Zaide.Features.Language.Infrastructure.Lsp;
using Zaide.ViewModels;
using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Contracts;
using Zaide.Features.Workspace.Domain;
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Editor.Domain;
using Zaide.Features.Editor.Infrastructure;
using Zaide.Features.Editor.Presentation;

using Zaide.Features.Language.Contracts;
using Zaide.Features.Language.Application;
namespace Zaide.Tests.Features.Editor.Infrastructure;

/// <summary>
/// Command registry and keybinding tests for <c>editor.formatDocument</c>.
/// </summary>
public sealed class FormatDocumentCommandTests
{
    static FormatDocumentCommandTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    [Fact]
    public void FormatDocument_IsRegistered_WithCtrlShiftI()
    {
        var registry = CommandRegistryFactory.Create();
        var workspace = new global::Zaide.Features.Workspace.Domain.Workspace();
        var bridge = new EmptyBridge();
        var sessionService = new EmptySessionService();
        var services = new ServiceCollection()
            .AddSingleton(workspace)
            .AddSingleton<IFileService>(new FileService())
            .AddTransient(sp => new EditorViewModel(new Document(""), sp.GetRequiredService<IFileService>()))
            .BuildServiceProvider();
        var tabs = new EditorTabViewModel(services, services.GetRequiredService<IFileService>(), workspace);

        _ = new EditorLanguageInputViewModel(
            new LanguageCompletionService(workspace, sessionService, bridge, NullLogger<LanguageCompletionService>.Instance),
            new LanguageHoverService(workspace, sessionService, bridge, NullLogger<LanguageHoverService>.Instance),
            new LanguageNavigationService(workspace, sessionService, bridge, NullLogger<LanguageNavigationService>.Instance),
            new LanguageSymbolService(workspace, sessionService, bridge, NullLogger<LanguageSymbolService>.Instance),
            new LanguageFormattingService(workspace, sessionService, bridge, NullLogger<LanguageFormattingService>.Instance),
            sessionService,
            tabs,
            registry);

        var descriptor = registry.GetById(LanguageFormattingPolicy.FormatDocumentCommandId);
        Assert.NotNull(descriptor);
        Assert.Equal("Format Document", descriptor!.DisplayName);
        Assert.Equal("Editor", descriptor.Category);
        Assert.Equal(LanguageFormattingPolicy.FormatDocumentDefaultGestures, descriptor.DefaultGestures);
        Assert.Equal(new[] { "Ctrl+Shift+I" }, descriptor.DefaultGestures);

        var settings = new Mock<ISettingsService>();
        settings.SetupGet(s => s.Current).Returns(SettingsModel.Defaults);
        var bindings = registry.ResolveKeyBindings(settings.Object);
        Assert.Contains(bindings, b =>
            b.CommandId == LanguageFormattingPolicy.FormatDocumentCommandId &&
            b.Gesture == "Ctrl+Shift+I");
    }

    private sealed class EmptyBridge : ILanguageDocumentBridge
    {
        public bool TryGetOpenDocument(string documentUri, out LanguageTrackedDocumentInfo info)
        {
            info = default;
            return false;
        }

        public void Dispose() { }
    }

    private sealed class EmptySessionService : ILanguageSessionService
    {
        public LanguageSessionSnapshot Current { get; } = new(
            LanguageSessionState.Unavailable, 0, null, null, null, null);

        public IObservable<LanguageSessionSnapshot> WhenChanged { get; } =
            System.Reactive.Linq.Observable.Empty<LanguageSessionSnapshot>();

        public ILanguageServerSession? TryGetReadySession(long generation) => null;
        public Task RestartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
