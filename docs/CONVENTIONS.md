# Coding Conventions

Rules so all Zaide code reads like one person wrote it.

---

## Naming

| Thing | Case | Example |
|-------|------|---------|
| Namespaces | PascalCase | `Zaide.Services`, `Zaide.ViewModels` |
| Classes / Structs | PascalCase | `AgentRouter`, `TownhallEntry` |
| Interfaces | `I` + PascalCase | `IAgent`, `IPanel` |
| Methods | PascalCase | `RouteMessage()`, `OpenDocument()` |
| Properties | PascalCase | `IsActive`, `AgentName` |
| private fields | `_camelCase` | `_agents`, `_isDisposed` |
| local vars | camelCase | `fileName`, `panelIndex` |
| Constants | PascalCase | `MaxAgentCount` |

## Files

- One class per file (exceptions: small related records/enums)
- File name = class name: `AgentRouter.cs`, `IAgent.cs`
- XAML: `Foo.axaml` + `Foo.axaml.cs` (code-behind minimal)

## Namespaces

Must match folder structure:
```
src/Services/AgentRouter.cs  →  namespace Zaide.Services
src/ViewModels/MainWindowViewModel.cs → namespace Zaide.ViewModels
```

## MVVM — ReactiveUI

- **Framework:** ReactiveUI (chosen over CommunityToolkit.Mvvm for reactive pipelines).
- **ViewModels** never reference Views directly — only data binding via `WhenAnyValue`, `Bind`, `OneWayBind`.
- **Services** never reference ViewModels or Views.
- **Models** are plain data — no UI logic.
- Code-behind (`*.axaml.cs`) should be minimal — just `InitializeComponent()` if XAML exists, but prefer C# views per `DESIGN.md` Rule 1.
- **Activation:** Use `WhenActivated` for setup/teardown; dispose subscriptions with `.DisposeWith(d)`.
- **Commands:** All user actions via `ReactiveCommand.CreateFromTask` or `ReactiveCommand.Create`.

## Async

- Suffix async methods with `Async`: `Task OpenDocumentAsync()`
- Avoid `async void` except Avalonia event handlers
- Use `CancellationToken` on any I/O-bound method

## Nullability

- Project-wide nullable enabled (`<Nullable>enable</Nullable>`)
- Use `?` only when null is genuinely valid
- Prefer `?? throw new InvalidOperationException(...)` over `!` (null-forgiving)

## Formatting

- 4 spaces indentation
- Opening brace on new line (Allman style)
- `var` when type is obvious: `var doc = new TextDocument();`
- Explicit type when not obvious: `string path = GetPath();`

## Commit Messages

```
area: short imperative summary
```

Examples: `layout: add 3-panel grid`, `agents: implement townhall logger`, `docs: add phase-1 plan`

---

*Last updated: 2025-06-25*
