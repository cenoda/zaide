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
| **Avalonia.AvaloniaEdit** | Modern replacement for the deprecated `AvaloniaEdit` v0.10.12. Full code editor widget — text rendering, cursor, selection, line numbers, folding. | Without it, you're building from a `<TextBox>`. Months saved. v12.0.0 targets Avalonia 12 and .NET 8+/10. Pulled in transitively by `AvaloniaEdit.TextMate`. |
| **AvaloniaEdit.TextMate** | Teaches AvaloniaEdit to read TextMate grammars (VS Code's format for coloring). | Drop in `.tmLanguage` files → instant syntax highlighting. v12.0.0 compatible with Avalonia 12. |
| **TextMateSharp.Grammars** | Bundle of 100+ pre-made TextMate grammars. | C#, Python, JS, Rust, Go — all covered without hunting for grammar files. v2.0.4. |

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

## PERSISTENCE & DATA (Phase 0+)

| Library | What It Does | Why You Want It |
|---------|-------------|-----------------|
| **Microsoft.Data.Sqlite** | .NET's official SQLite binding. | Single-file database for townhall logs, agent history, project metadata. |
| **Dapper** | Lightweight micro-ORM (optional). | Simple query mapping without Entity Framework overhead. |

## ICONS & ASSETS

Zaide targets **Avalonia 12.0.5** on **.NET 10**. Icons are served through a
catalogued NuGet icon pack behind the `IconFactory` facade (see `docs-rules.md`
§8: use a library when it covers 80%+ of the need).

### In use

| Library | What It Does | Why You Want It | Stack notes (2026-08) |
|---------|-------------|-----------------|------------------------|
| **Lucide.Avalonia** | Lucide stroke icons as `LucideIcon` controls and `{LucideIconContent …}` markup for Avalonia. MIT. | Stroke-oriented glyphs designed for ~16px UI; weekly icon updates; C# `Content = …` without AXAML xmlns. | NuGet **0.2.16** (pinned in `Directory.Packages.props`). Used via `App/Shell/IconFactory` + `IconLucideMap` — features do not reference Lucide types directly. Repo: [dme-compunet/Lucide.Avalonia](https://github.com/dme-compunet/Lucide.Avalonia). |

**Integration shape:**
- `IconFactory.Create("Icon.*", brush, size)` and `FileIconKeyResolver` keys stay stable.
- `IconLucideMap` maps `Icon.*` → `LucideIconKind` inside `App/Shell`.
- NavBar uses the same `IconFactory` pipeline (`Icon.Explorer`, `Icon.SourceControl`).

### Fallback (not in use)

| Library | What It Does | Status |
|---------|-------------|--------|
| **IconPacks.Avalonia** (MahApps) | Aggregates many sets (Material, FontAwesome, Lucide, Phosphor, …) as `PackIcon` controls. MIT. | **Fallback only** — Avalonia 12 stable **1.3.x** reported runtime failures ([issue #41](https://github.com/MahApps/IconPacks.Avalonia/issues/41)); verify a released Avalonia 12 build before adopting. Heavier than a single-set pack. |

### Removed — embedded Phosphor (Phase 23 F10)

| Asset | Status |
|-------|--------|
| **Phosphor Icons (`StreamGeometry` in `Icons.axaml`)** | **Removed** (2026-08-06, F10). Insufficient at 14–20px. Attribution: MIT — Copyright (c) 2023 Phosphor Icons. |

## LANGUAGE INTELLIGENCE (Phase 10)

| Library | What It Does | Why You Want It |
|---------|-------------|-----------------|
| **csharp-ls** (global `dotnet tool`, not a NuGet app dependency) | Roslyn-based C# language server speaking LSP over stdio. Version proven at M0: **0.25.0**. License: MIT. | Process-backed C# diagnostics, completion, hover, definition, symbols, and document formatting without embedding Roslyn UI. Acquisition: `dotnet tool install -g csharp-ls` (no repository-wide SDK reorganization). Selected in Phase 10 M0 proof. |
| **StreamJsonRpc** | Content-Length-framed JSON-RPC library used to speak LSP over stdio (and other transports). Version pinned: **2.22.23**. License: MIT. | Production language-session transport in `LanguageSessionService` / `CsharpLsSession`. Central pin in `Directory.Packages.props`; referenced from `src/Zaide.csproj`. |
| **MessagePack** | Binary serialization dependency of StreamJsonRpc. Version pinned: **3.1.8** (central override of StreamJsonRpc's older transitive). License: MIT. | Keeps StreamJsonRpc on a single audited MessagePack revision via explicit product reference. |

---

## Technical Decisions (Resolved)

| Decision | Choice | Rationale |
|----------|--------|-----------|
| **Backend** | SQLite + JSON (settings) | Time-series data needs queries. One-file management. |
| **Image Storage** | Hybrid: Embedded (UI) + File Ref (project) | Icons build in, agent avatars swap at runtime. |
| **Plugin** | Interface + DI manual registration | Core interfaces now, .NET 10 Keyed Services later. |

---

## Adding a Library

1. Check this file first — is it already catalogued?
2. If not, add it here with: What It Does, Why You Want It, Phase
3. Add to `src/Zaide.csproj` (version pinned centrally in `Directory.Packages.props`)
4. Verify it builds: `dotnet build Zaide.slnx`

---

*Last updated: 2026-08-06 (Phase 23 F10 — icon pack direction accepted; Lucide.Avalonia primary candidate; embedded Phosphor interim deprecated. Prior: 2026-07-17 Refactor 6.2 M1–M12 — no NuGet change. Phase 10 stack: csharp-ls 0.25.0 + StreamJsonRpc 2.22.23)*
