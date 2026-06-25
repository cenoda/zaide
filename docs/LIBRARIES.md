# Library Catalog for Zaide

Every library explained in plain English — what it does, why you'd want it, and when it matters.

---

## UI FRAMEWORK (Phase 0)

| Library | What It Does | Why You Want It |
|---------|-------------|-----------------|
| **Avalonia 12** | Cross-platform XAML UI framework for .NET. | The foundation — renders windows, controls, layouts on Linux/macOS/Windows. |
| **Semi.Avalonia** | Dark theme and component library for Avalonia. | Consistent dark-mode look without designing every control from scratch. |
| **Semi.Avalonia.DataGrid** | Data grid component from Semi design system. | Structured tabular data display (logs, git status, etc). |

## EDITOR & TEXT (Phase 2)

| Library | What It Does | Why You Want It |
|---------|-------------|-----------------|
| **AvaloniaEdit** | Real code editor widget — text rendering, cursor, selection, line numbers, folding. | Without it, you're building from a `<TextBox>`. Months saved. Must verify Avalonia 12 compatibility. |
| **AvaloniaEdit.TextMate** | Teaches AvaloniaEdit to read TextMate grammars (VS Code's format for coloring). | Drop in `.tmLanguage` files → instant syntax highlighting. |
| **TextMateSharp.Grammars** | Bundle of 100+ pre-made TextMate grammars. | C#, Python, JS, Rust, Go — all covered without hunting for grammar files. |

## TERMINAL (Phase 3)

| Library | What It Does | Why You Want It |
|---------|-------------|-----------------|
| **Pty.Net** | Wraps OS pseudo-terminals (Linux ptmx, Windows ConPTY). | Programs like git, htop need a real pty or their output breaks. |
| **VtNetCore** | Parses VT100/xterm escape codes into structured data. | Renders colored/formatted terminal output correctly. |
| **CliWrap** | Clean wrapper around Process.Start. | Simple run-to-completion commands without boilerplate. |

## MVVM & REACTIVE (Phase 0)

| Library | What It Does | Why You Want It |
|---------|-------------|-----------------|
| **ReactiveUI.Avalonia** | ReactiveUI for Avalonia — `WhenActivated`, reactive bindings, activation, `RoutedViewHost`. | Chosen MVVM framework. Replaces deprecated `Avalonia.ReactiveUI`. v12 targets Avalonia ≥ 12.0.4. |
| **ReactiveUI.Avalonia.Microsoft.Extensions.DependencyInjection** | Bridges ReactiveUI/Splat to `IServiceCollection`. | Wires our MS DI container into ReactiveUI's service resolution (`UseReactiveUIWithMicrosoftDependencyResolver`). |
| ~~CommunityToolkit.Mvvm~~ | ~~Lightweight MVVM — source generators.~~ | Not chosen. |

## DI & CONFIG (Phase 0+)

| Library | What It Does | Why You Want It |
|---------|-------------|-----------------|
| **Microsoft.Extensions.DependencyInjection** | .NET's built-in DI container. | Constructor injection, lifetime management, no service locators. |
| **Microsoft.Extensions.Logging** | Structured logging abstraction. | Switch between console/file/trace output without changing code. |

## GIT (Phase 7)

| Library | What It Does | Why You Want It |
|---------|-------------|-----------------|
| **LibGit2Sharp** | Full git in .NET — status, diff, log, branches, commits. | Typed objects instead of parsing CLI output. |
| **DiffPlex** | Diff algorithm — which lines were added/removed/changed. | Git diff view + unsaved changes comparison. |

---

## Adding a Library

1. Check this file first — is it already catalogued?
2. If not, add it here with: What It Does, Why You Want It, Phase
3. Add to `Zaide.csproj` with a pinned version
4. Verify it builds: `dotnet build`

---

*Last updated: 2025-06-25*
