using System;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Services;
using Zaide.UI.DesignSystem;
using Zaide.ViewModels;
using Zaide.Features.Workspace.Domain;
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Editor.Infrastructure;
using Zaide.Features.Editor.Presentation;

namespace Zaide.Tests.Features.Editor.Presentation;

/// <summary>
/// ISSUE-007 regression: UnsavedDialog must not bind <c>Margin</c> (Thickness)
/// to Double spacing tokens, and dirty-tab close must drive ConfirmClose
/// outcomes correctly (Save / Don't Save / Cancel / clean skip).
/// </summary>
/// <remarks>
/// Full <c>new UnsavedDialog()</c> requires Avalonia windowing platform + UI
/// thread (not available under the project's xunit2 suite without Headless.XUnit
/// v3 conflicts). Construction is covered by XAML source guard + manual Linux
/// smoke; ConfirmClose semantics are covered here.
/// </remarks>
public sealed class UnsavedDialogTests
{
    static UnsavedDialogTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
        EnsureApplication();
    }

    [Fact]
    public void UnsavedDialogAxaml_DoesNotBindMarginToDoubleSpacingToken()
    {
        // Production crash: Margin="{StaticResource SpacingXl}" with SpacingXl as
        // x:Double → InvalidCastException in compiled Avalonia XAML setter.
        var axamlPath = FindRepoFile(Path.Combine("src", "Features", "Editor", "Presentation", "UnsavedDialog.axaml"));
        var text = File.ReadAllText(axamlPath);

        Assert.DoesNotMatch(
            new Regex(@"Margin\s*=\s*""\{StaticResource\s+Spacing\w+\s*\}""", RegexOptions.IgnoreCase),
            text);

        // Spacing (double) tokens remain valid on StackPanel.Spacing.
        Assert.Contains("Spacing=\"{StaticResource SpacingLg}\"", text);
        Assert.Contains("Spacing=\"{StaticResource SpacingSm}\"", text);
    }

    [Fact]
    public void SpacingXl_Resource_IsDouble_NotThickness()
    {
        var app = EnsureApplication();
        Assert.True(app.Resources.TryGetValue("SpacingXl", out var value));
        Assert.IsType<double>(value);
        Assert.Equal(20d, (double)value!);
    }

    [Fact]
    public void CodeBehindMargin_UsesThicknessFromSpacingXlToken()
    {
        // Mirrors UnsavedDialog ctor after InitializeComponent.
        var margin = LayoutTokens.Uniform(LayoutTokens.SpacingXl);
        Assert.Equal(LayoutTokens.SpacingXl, margin.Left);
        Assert.Equal(LayoutTokens.SpacingXl, margin.Top);
        Assert.Equal(LayoutTokens.SpacingXl, margin.Right);
        Assert.Equal(LayoutTokens.SpacingXl, margin.Bottom);
    }

    [Fact]
    public void UnsavedDialogCodeBehind_AppliesLayoutTokensMargin()
    {
        var csPath = FindRepoFile(Path.Combine("src", "Features", "Editor", "Presentation", "UnsavedDialog.axaml.cs"));
        var text = File.ReadAllText(csPath);

        Assert.Contains("RootPanel.Margin = LayoutTokens.Uniform(LayoutTokens.SpacingXl)", text);
    }

    [Fact]
    public async Task DirtyTabClose_InvokesConfirmClose_SaveWritesAndRemovesTab()
    {
        var path = Path.Combine(Path.GetTempPath(), "zaide-unsaved-" + Guid.NewGuid() + ".txt");
        File.WriteAllText(path, "original");

        try
        {
            var vm = CreateTabManager();
            var handlerCalled = false;
            vm.ConfirmClose.RegisterHandler(ctx =>
            {
                handlerCalled = true;
                ctx.SetOutput(true); // Save
            });

            await vm.OpenFileCommand.Execute(path);
            var tab = vm.OpenTabs[0];
            tab.TextContent = "saved-from-dialog";
            Assert.True(tab.IsDirty);

            await vm.CloseTabCommand.Execute(tab);

            Assert.True(handlerCalled, "Dirty close must raise ConfirmClose");
            Assert.Empty(vm.OpenTabs);
            Assert.Equal("saved-from-dialog", File.ReadAllText(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task DirtyTabClose_DontSave_RemovesTabWithoutWriting()
    {
        var path = Path.Combine(Path.GetTempPath(), "zaide-unsaved-" + Guid.NewGuid() + ".txt");
        File.WriteAllText(path, "original");

        try
        {
            var vm = CreateTabManager();
            var handlerCalled = false;
            vm.ConfirmClose.RegisterHandler(ctx =>
            {
                handlerCalled = true;
                ctx.SetOutput(false); // Don't Save
            });

            await vm.OpenFileCommand.Execute(path);
            var tab = vm.OpenTabs[0];
            tab.TextContent = "discarded";
            Assert.True(tab.IsDirty);

            await vm.CloseTabCommand.Execute(tab);

            Assert.True(handlerCalled, "Dirty close must raise ConfirmClose");
            Assert.Empty(vm.OpenTabs);
            Assert.Equal("original", File.ReadAllText(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task DirtyTabClose_Cancel_LeavesTabOpenAndDirty()
    {
        var path = Path.Combine(Path.GetTempPath(), "zaide-unsaved-" + Guid.NewGuid() + ".txt");
        File.WriteAllText(path, "original");

        try
        {
            var vm = CreateTabManager();
            var handlerCalled = false;
            vm.ConfirmClose.RegisterHandler(ctx =>
            {
                handlerCalled = true;
                ctx.SetOutput(null); // Cancel
            });

            await vm.OpenFileCommand.Execute(path);
            var tab = vm.OpenTabs[0];
            tab.TextContent = "still-editing";
            Assert.True(tab.IsDirty);

            await vm.CloseTabCommand.Execute(tab);

            Assert.True(handlerCalled, "Dirty close must raise ConfirmClose");
            Assert.Single(vm.OpenTabs);
            Assert.Same(tab, vm.OpenTabs[0]);
            Assert.True(tab.IsDirty);
            Assert.Equal("original", File.ReadAllText(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task CleanTabClose_DoesNotInvokeConfirmClose()
    {
        var path = Path.Combine(Path.GetTempPath(), "zaide-unsaved-" + Guid.NewGuid() + ".txt");
        File.WriteAllText(path, "clean");

        try
        {
            var vm = CreateTabManager();
            var handlerCalled = false;
            vm.ConfirmClose.RegisterHandler(ctx =>
            {
                handlerCalled = true;
                ctx.SetOutput(true);
            });

            await vm.OpenFileCommand.Execute(path);
            var tab = vm.OpenTabs[0];
            Assert.False(tab.IsDirty);

            await vm.CloseTabCommand.Execute(tab);

            Assert.False(handlerCalled, "Clean tab must not raise ConfirmClose");
            Assert.Empty(vm.OpenTabs);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static EditorTabViewModel CreateTabManager()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFileService, FileService>();
        services.AddTransient<EditorViewModel>();
        services.AddSingleton<global::Zaide.Features.Workspace.Domain.Workspace>();
        var sp = services.BuildServiceProvider();
        return new EditorTabViewModel(
            sp,
            sp.GetRequiredService<IFileService>(),
            sp.GetRequiredService<global::Zaide.Features.Workspace.Domain.Workspace>());
    }

    private static App EnsureApplication()
    {
        if (Application.Current is App current)
        {
            if (!current.Resources.ContainsKey("SpacingXl"))
                current.Initialize();
            return current;
        }

        var app = new App();
        app.Initialize();
        return app;
    }

    private static string FindRepoFile(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        // Fallback: walk from current working directory.
        var cwd = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (cwd is not null)
        {
            var candidate = Path.Combine(cwd.FullName, relativePath);
            if (File.Exists(candidate))
                return candidate;
            cwd = cwd.Parent;
        }

        throw new FileNotFoundException($"Could not locate {relativePath}");
    }
}
