# Phase 2.1 Indent Guides Implementation Plan COMPLETED

## Entry Gate (M0) - COMPLETED ✓
- Build: 0 warnings, 0 errors
- Tests: 69 passing, 0 failures

## Implementation Tasks - ALL COMPLETED ✓

### Task 1: Create IndentGuideRenderer.cs ✓
- [x] Implement `IBackgroundRenderer` interface
- [x] Layer property returns `KnownLayer.Background`
- [x] IsEnabled property (bool) - toggle for renderer
- [x] Static `ShouldEnableForFile(path)` - uses same extension set as GetGrammarScope
- [x] Static `GetIndentDepth(line, tabWidth)` - computes indent depth
- [x] Draw() renders vertical dotted lines at each indent level

### Task 2: Modify EditorView.cs ✓
- [x] Added field: `_indentGuideRenderer`
- [x] Registered in constructor
- [x] Enabled in ApplyFileMode()

### Task 3: Create Unit Tests ✓
- [x] GetIndentDepth tests: spaces, tabs, mixed, empty, no-indent
- [x] ShouldEnableForFile tests: supported/unsupported extensions

### Exit Gate - COMPLETED ✓
- [x] Build: 0 warnings, 0 errors
- [x] Tests: 90 passing, 0 failures (+21 new tests)

## Summary
Phase 2.1 indent guides feature fully implemented:
- New files: IndentGuideRenderer.cs, IndentGuideRendererTests.cs
- Modified files: EditorView.cs (+3 lines integration)
- All tests pass
