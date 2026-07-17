using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI;
using ReactiveUI.Builder;
using Xunit;
using Zaide.Services;
using Zaide.ViewModels;
using Zaide.Views;
using Zaide.Features.Workspace.Domain;
using Zaide.Features.Editor.Contracts;
using Zaide.Features.Editor.Domain;
using Zaide.Features.Editor.Infrastructure;
using Zaide.Features.Editor.Presentation;

namespace Zaide.Tests.Features.Editor.Presentation;

/// <summary>
/// Phase 9 M4: Tests for <see cref="BraceFoldingStrategy"/> discovery,
/// command registration, folding operations, tab-switch safety, and
/// caret/selection validity.
/// </summary>
public sealed class EditorFoldingTests
{
    static EditorFoldingTests()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }

    private static ICommandRegistry CreateRegistry() => CommandRegistryFactory.Create();

    // ═══════════════════════════════════════════════════════════════════════
    // BraceFoldingStrategy — Pure Algorithm Tests (no Avalonia controls)
    // ═══════════════════════════════════════════════════════════════════════

    public sealed class BraceFoldingStrategyTests
    {
        [Fact]
        public void Discover_EmptyText_ReturnsEmpty()
        {
            var result = BraceFoldingStrategy.Discover("");
            Assert.Empty(result);
        }

        [Fact]
        public void Discover_NullText_ReturnsEmpty()
        {
            var result = BraceFoldingStrategy.Discover(null!);
            Assert.Empty(result);
        }

        [Fact]
        public void Discover_PlainTextWithNoBraces_ReturnsEmpty()
        {
            var result = BraceFoldingStrategy.Discover("hello world\nno braces here");
            Assert.Empty(result);
        }

        [Fact]
        public void Discover_SimpleBalancedBraces_ReturnsOneRegion()
        {
            var text = "{\n  content\n}";
            var result = BraceFoldingStrategy.Discover(text);

            Assert.Single(result);
            Assert.Equal(0, result[0].StartOffset);
            Assert.Equal(text.Length, result[0].EndOffset);
            Assert.Equal(0, result[0].Depth);
        }

        [Fact]
        public void Discover_NestedBraces_ReturnsOuterAndInner()
        {
            var text = "{\n  outer\n  {\n    inner\n  }\n}";
            var result = BraceFoldingStrategy.Discover(text);

            Assert.Equal(2, result.Count);
            // Sorted by start offset: outer (offset 0) before inner.
            var outer = result[0];
            var inner = result[1];
            Assert.Equal(0, outer.StartOffset);
            Assert.Equal(text.Length, outer.EndOffset);
            Assert.Equal(0, outer.Depth);

            Assert.True(inner.StartOffset > outer.StartOffset);
            Assert.True(inner.EndOffset < outer.EndOffset);
            Assert.Equal(1, inner.Depth);
        }

        [Fact]
        public void Discover_MultipleSiblingRegions_ReturnsAllInStartOrder()
        {
            var text = "{\n  a\n}\n{\n  b\n}";
            var result = BraceFoldingStrategy.Discover(text);

            Assert.Equal(2, result.Count);
            Assert.True(result[0].StartOffset < result[1].StartOffset);
            Assert.Equal(0, result[0].Depth);
            Assert.Equal(0, result[1].Depth);
        }

        [Fact]
        public void Discover_UnmatchedOpenBraces_AreIgnored()
        {
            // Two open braces, only one close. Need enough newlines to meet
            // MinRegionLines=2 between the paired braces.
            var text = "{ unmatched\n\n{ paired\n\n}\n";
            var result = BraceFoldingStrategy.Discover(text);

            Assert.Single(result);
            var pairedOpen = text.IndexOf('{', 1);
            Assert.Equal(pairedOpen, result[0].StartOffset);
        }

        [Fact]
        public void Discover_UnmatchedCloseBraces_AreIgnored()
        {
            // One balanced pair plus an extra unmatched close brace.
            var text = "{ valid\n\n}\n\n} extra";
            var result = BraceFoldingStrategy.Discover(text);

            Assert.Single(result);
            Assert.Equal(0, result[0].StartOffset);
        }

        [Fact]
        public void Discover_TooShortRegion_SingleLine_ReturnsEmpty()
        {
            // Single line: open and close on same line = fewer than MinRegionLines.
            var text = "{ single line }";
            var result = BraceFoldingStrategy.Discover(text);

            Assert.Empty(result);
        }

        [Fact]
        public void Discover_TooShortRegion_AdjacentLines_ReturnsEmpty()
        {
            // Only one newline between { and } = 1 line count.
            var text = "{\n}";
            var result = BraceFoldingStrategy.Discover(text);

            Assert.Empty(result);
        }

        [Fact]
        public void Discover_MinimumRegionLines_ReturnsRegion()
        {
            // Exactly 2 newlines before the closing brace.
            var text = "{\n\n}";
            var result = BraceFoldingStrategy.Discover(text);

            Assert.Single(result);
        }

        [Fact]
        public void Discover_DeeplyNested_ReturnsAllLevels()
        {
            var text = "{\n  {\n    {\n      {\n        deep\n      }\n    }\n  }\n}";
            var result = BraceFoldingStrategy.Discover(text);

            Assert.Equal(4, result.Count);
            // Sorted by start offset — outermost first.
            Assert.Equal(0, result[0].Depth);
            Assert.Equal(1, result[1].Depth);
            Assert.Equal(2, result[2].Depth);
            Assert.Equal(3, result[3].Depth);
        }

        [Fact]
        public void Discover_TitleExtractedFromLineAfterBrace()
        {
            // { followed by \n immediately → title = "{...}"
            var text = "{\n  // setup\n  int x = 1;\n}";
            var result = BraceFoldingStrategy.Discover(text);

            Assert.Single(result);
            Assert.Equal("{...}", result[0].Title);
        }

        [Fact]
        public void Discover_TitleExtractedFromSameLine()
        {
            // { with text on same line → title from that text.
            var text = "{ // class MyClass\n  void Method() { }\n\n}";
            var result = BraceFoldingStrategy.Discover(text);

            Assert.Single(result);
            Assert.Equal("// class MyClass", result[0].Title);
        }

        [Fact]
        public void Discover_TitleTruncatedAtMaxLength()
        {
            var longText = new string('x', 200);
            var text = "{ " + longText + "\n  content\n\n}";
            var result = BraceFoldingStrategy.Discover(text);

            Assert.Single(result);
            Assert.Equal(BraceFoldingStrategy.MaxTitleLength, result[0].Title.Length);
        }

        [Fact]
        public void Discover_MixedTabsAndSpaces_TitleTrimmed()
        {
            // Need MinRegionLines=2, so add an extra blank line.
            var text = "{\t\t  private int _field;\n\n}";
            var result = BraceFoldingStrategy.Discover(text);

            Assert.Single(result);
            Assert.Equal("private int _field;", result[0].Title);
        }

        [Fact]
        public void Discover_OrderIsDeterministic()
        {
            var text = "{\n  a\n}\n{\n  b\n}\n{\n  c\n}";
            var result1 = BraceFoldingStrategy.Discover(text);
            var result2 = BraceFoldingStrategy.Discover(text);

            Assert.Equal(result1.Count, result2.Count);
            for (var i = 0; i < result1.Count; i++)
            {
                Assert.Equal(result1[i].StartOffset, result2[i].StartOffset);
                Assert.Equal(result1[i].EndOffset, result2[i].EndOffset);
                Assert.Equal(result1[i].Title, result2[i].Title);
            }
        }

        [Fact]
        public void Discover_CRLF_HandlesWindowsNewlines()
        {
            var text = "{\r\n  content\r\n\r\n}";
            var result = BraceFoldingStrategy.Discover(text);

            Assert.Single(result);
            Assert.Equal(0, result[0].StartOffset);
            Assert.Equal(text.Length, result[0].EndOffset);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BraceRegion FindInnermostContaining Tests
    // ═══════════════════════════════════════════════════════════════════════

    public sealed class FindInnermostContainingTests
    {
        [Fact]
        public void CaretInsideRegion_ReturnsRegion()
        {
            var text = "{\n  content\n\n}";
            var regions = BraceFoldingStrategy.Discover(text);
            Assert.Single(regions);
            var caretOffset = 5; // inside the region
            var result = BraceFoldingStrategy.FindInnermostContaining(regions, caretOffset);

            Assert.NotNull(result);
            Assert.Equal(0, result!.StartOffset);
        }

        [Fact]
        public void CaretOutsideRegion_ReturnsNull()
        {
            var text = "before\n\n{\n  content\n}\nafter";
            var regions = BraceFoldingStrategy.Discover(text);
            var caretOffset = 2; // in "before"
            var result = BraceFoldingStrategy.FindInnermostContaining(regions, caretOffset);

            Assert.Null(result);
        }

        [Fact]
        public void CaretInNested_ReturnsInnermost()
        {
            var text = "{\n  outer\n  {\n    inner\n  }\n}";
            var regions = BraceFoldingStrategy.Discover(text);
            Assert.Equal(2, regions.Count);
            // Sorted by start offset: [0]=outer, [1]=inner.
            var innerRegion = regions[1];
            Assert.Equal(1, innerRegion.Depth);
            var caretOffset = innerRegion.StartOffset + 5;
            var result = BraceFoldingStrategy.FindInnermostContaining(regions, caretOffset);

            Assert.NotNull(result);
            Assert.Equal(innerRegion.StartOffset, result!.StartOffset);
            Assert.Equal(1, result.Depth);
        }

        [Fact]
        public void CaretAtStartOffset_IsContained()
        {
            var text = "{\n  content\n\n}";
            var regions = BraceFoldingStrategy.Discover(text);
            var caretOffset = 0; // exactly at '{'
            var result = BraceFoldingStrategy.FindInnermostContaining(regions, caretOffset);

            Assert.NotNull(result);
        }

        [Fact]
        public void EmptyRegions_ReturnsNull()
        {
            var result = BraceFoldingStrategy.FindInnermostContaining(
                Array.Empty<BraceRegion>(), 10);
            Assert.Null(result);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Command Registration and Metadata Tests
    // ═══════════════════════════════════════════════════════════════════════

    public sealed class FoldingCommandRegistrationTests
    {
        private static EditorTabViewModel CreateEditorTabs(ICommandRegistry registry)
        {
            var sp = new ServiceCollection()
                .AddSingleton<IFileService>(new FileService())
                .AddSingleton<global::Zaide.Features.Workspace.Domain.Workspace>()
                .BuildServiceProvider();

            return new EditorTabViewModel(
                sp,
                sp.GetRequiredService<IFileService>(),
                sp.GetRequiredService<global::Zaide.Features.Workspace.Domain.Workspace>(),
                registry);
        }

        [Fact]
        public void FoldToggle_RegisteredWithCorrectMetadata()
        {
            var registry = CreateRegistry();
            CreateEditorTabs(registry);

            var descriptor = registry.GetById("editor.foldToggle");
            Assert.NotNull(descriptor);
            Assert.Equal("editor.foldToggle", descriptor!.Id);
            Assert.Equal("Toggle Current Fold", descriptor.DisplayName);
            Assert.Equal("Editor", descriptor.Category);
            Assert.Empty(descriptor.DefaultGestures);
        }

        [Fact]
        public void FoldAll_RegisteredWithCorrectMetadata()
        {
            var registry = CreateRegistry();
            CreateEditorTabs(registry);

            var descriptor = registry.GetById("editor.foldAll");
            Assert.NotNull(descriptor);
            Assert.Equal("Fold All", descriptor!.DisplayName);
            Assert.Equal("Editor", descriptor.Category);
            Assert.Empty(descriptor.DefaultGestures);
        }

        [Fact]
        public void UnfoldAll_RegisteredWithCorrectMetadata()
        {
            var registry = CreateRegistry();
            CreateEditorTabs(registry);

            var descriptor = registry.GetById("editor.unfoldAll");
            Assert.NotNull(descriptor);
            Assert.Equal("Unfold All", descriptor!.DisplayName);
            Assert.Equal("Editor", descriptor.Category);
            Assert.Empty(descriptor.DefaultGestures);
        }

        [Fact]
        public void AllThreeCommands_RegisteredExactlyOnce()
        {
            var registry = CreateRegistry();
            CreateEditorTabs(registry);

            Assert.Equal(1, registry.GetAll().Count(d => d.Id == "editor.foldToggle"));
            Assert.Equal(1, registry.GetAll().Count(d => d.Id == "editor.foldAll"));
            Assert.Equal(1, registry.GetAll().Count(d => d.Id == "editor.unfoldAll"));
        }

        [Fact]
        public void DuplicateRegistration_Throws()
        {
            var registry = CreateRegistry();
            CreateEditorTabs(registry);

            Assert.Throws<InvalidOperationException>(() => CreateEditorTabs(registry));
        }

        [Fact]
        public void FoldToggle_NotAvailable_WhenNoActiveTab()
        {
            var registry = CreateRegistry();
            var tabs = CreateEditorTabs(registry);
            tabs.ActiveTab = null;
            tabs.FoldingEditor = null;

            var descriptor = registry.GetById("editor.foldToggle");
            Assert.False(descriptor!.Command.CanExecute(null));
        }

        [Fact]
        public void FoldToggle_NotAvailable_WhenNoFoldingEditor()
        {
            var registry = CreateRegistry();
            var tabs = CreateEditorTabs(registry);
            var vm = new EditorViewModel(new Document(""), new FileService());
            tabs.OpenTabs.Add(vm);
            tabs.ActiveTab = vm;
            tabs.FoldingEditor = null;

            var descriptor = registry.GetById("editor.foldToggle");
            Assert.False(descriptor!.Command.CanExecute(null));
        }

        [Fact]
        public void FoldAll_NotAvailable_WhenNoActiveTab()
        {
            var registry = CreateRegistry();
            var tabs = CreateEditorTabs(registry);
            tabs.ActiveTab = null;
            tabs.FoldingEditor = null;

            var descriptor = registry.GetById("editor.foldAll");
            Assert.False(descriptor!.Command.CanExecute(null));
        }

        [Fact]
        public void UnfoldAll_NotAvailable_WhenNoActiveTab()
        {
            var registry = CreateRegistry();
            var tabs = CreateEditorTabs(registry);
            tabs.ActiveTab = null;
            tabs.FoldingEditor = null;

            var descriptor = registry.GetById("editor.unfoldAll");
            Assert.False(descriptor!.Command.CanExecute(null));
        }

        [Fact]
        public void FoldingCommands_DefaultGesturesAreUnbound()
        {
            var registry = CreateRegistry();
            CreateEditorTabs(registry);

            foreach (var id in new[] { "editor.foldToggle", "editor.foldAll", "editor.unfoldAll" })
            {
                var descriptor = registry.GetById(id);
                Assert.NotNull(descriptor);
                Assert.Empty(descriptor!.DefaultGestures);
            }
        }
    }
}
