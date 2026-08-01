# Phase 22.1 M0 Live-Seam Verification

## Status and Boundary

This is a read-only M0 record against `master` at
`938227b2c2fd743ac8f4d84b30ffdfac0500f6c1`. Before documentation edits,
`HEAD` and the freshly fetched `origin/master` were equal and the worktree was
clean.

No A3 scenario was re-run. The historical A3 outcomes remain owned by
`docs/audits/v1-v3-product-reality/evidence/A3_LANGUAGE_INTELLIGENCE.md`; this
record only reconciles those observations with the current production seams.
No production code, tests, packages, or A0-A3 evidence were changed.

M0 documentation is complete and awaits **human G2 acceptance**.
Implementation remains unauthorized.

## Live Ownership and Composition

| Concern | Live seam | Verified ownership |
|---------|-----------|--------------------|
| Production composition | `src/App/Composition/Program.cs` -> `Program.ConfigureServices(IServiceCollection)` | Calls `AddZaideAppCore()`, `AddZaideEditor()`, and `AddZaideLanguage()` once in the production container. |
| Application scheduler | `src/App/Composition/Registration/AppCoreServiceCollectionExtensions.cs` | Registers singleton `IScheduler` as `ReactiveUI.Avalonia.AvaloniaScheduler.Instance`. |
| Language DI | `src/App/Composition/Registration/LanguageServiceCollectionExtensions.cs` | Registers completion, hover, navigation, and symbol services as singleton interface-to-implementation mappings. The services do not receive `IScheduler`. |
| Editor DI | `src/App/Composition/Registration/EditorServiceCollectionExtensions.cs` | Registers singleton `EditorLanguageInputViewModel` and `IEditorUiDispatcher` -> `AvaloniaEditorUiDispatcher`. The language-input ViewModel does not receive either scheduling seam. |
| Shared presentation | `src/Features/Editor/Presentation/EditorView.cs` | One shared editor view subscribes directly to the four service observables and mutates Avalonia popup controls in its `Apply*Snapshot` methods. No `ObserveOn` or dispatcher boundary exists on those subscriptions. |
| ViewModel projection | `src/Features/Editor/Presentation/EditorLanguageInputViewModel.cs` | Exposes the raw completion/hover/navigation/symbol observables and also directly subscribes to navigation and symbol terminal states. It owns command routing and single-definition auto-navigation. |

The ownership boundary is therefore explicit: language application services may
complete on worker threads, while `EditorLanguageInputViewModel` and
`EditorView` own presentation and UI mutation. Production DI already owns an
Avalonia `IScheduler`, but the language-to-editor projection does not consume
it. `IEditorUiDispatcher` is also registered, but it is not used by these
language projection paths. M1-M3 must preserve service-level cancellation and
stale-result rules while introducing the smallest presentation-owned dispatch
boundary proven by their tests.

## End-to-End Traces and Failure Mechanisms

All five requests use `CsharpLsSession` and
`InvokeWithParameterObjectAsync<JsonElement?>` with the live LSP method and
parameter-object shape. Their parsers are
`LanguageServerCompletionParser`, `LanguageServerHoverParser`,
`LanguageServerDefinitionParser`, and `LanguageServerSymbolParser`.

### `A1-FN-09` / BL-01 - completion

1. `editor.triggerSuggest` reaches
   `EditorLanguageInputViewModel.TriggerCompletion` and
   `LanguageCompletionService.RequestExplicit`.
2. The service validates the active document, session generation, synchronized
   document version, and capability before publishing `Loading`.
3. `ExecuteRequestAsync` maps the caret to UTF-16, calls
   `CsharpLsSession.RequestCompletionAsync`, and resumes after
   `ConfigureAwait(false)`.
4. A non-empty result is mapped and synchronously published through
   `Subject.OnNext`.
5. `EditorView.ApplyCompletionSnapshot` receives that notification on the
   worker thread and mutates `EditorCompletionPopup`. Avalonia throws the
   thread-owner exception captured in A3. The service catches it as a request
   failure, which explains the `Failed` snapshot and zero usable items.

This is a projection thread-affinity failure after a Ready server response, not
a missing binary, capability, document-sync, or completion-parser failure.

### `A1-FN-10` / BL-02 - caret-dwell hover

1. `EditorView.OnTextChanged`/caret movement reaches
   `EditorLanguageInputViewModel.OnCaretMoved` and
   `LanguageHoverService.Schedule`.
2. `ScheduleAsync` waits for the 450 ms dwell with `ConfigureAwait(false)` and
   then calls `BeginRequest` on a worker thread.
3. `BeginRequest` sets the current state to `Loading` and synchronously calls
   `Subject.OnNext` before starting `ExecuteRequestAsync`.
4. `EditorView.ApplyHoverSnapshot` handles the non-visible `Loading` snapshot by
   setting the Avalonia popup's `IsOpen` property off-thread. That observer
   exception faults the fire-and-forget scheduling task before the LSP hover
   request is started, leaving the current state at `Loading` exactly as A3
   observed.

This is an earlier manifestation than completion: the unscheduled dwell
continuation can fail during the `Loading` publication itself.

### `A1-FN-11` / BL-03 - Go to Definition

1. `editor.goToDefinition` reaches
   `EditorLanguageInputViewModel.GoToDefinitionAsync` and
   `LanguageNavigationService.RequestDefinition`.
2. The service validates the active synchronized document, publishes `Loading`,
   maps the position, and calls `CsharpLsSession.RequestDefinitionAsync`.
3. The service has no request timeout or other terminalization deadline. A3's
   known-symbol request did not return before the harness deadline, so the
   service remained at the previously published `Loading` snapshot. The
   unresolved-symbol request did return and proved the Empty path separately.
4. If the known-symbol request later returns, `ConfigureAwait(false)` resumes
   on a worker thread and synchronously publishes its result.
5. `EditorLanguageInputViewModel.OnNavigationSnapshot` may auto-consume a
   single location and initiate tab navigation, while
   `EditorView.ApplyNavigationSnapshot` mutates the definition picker. Neither
   observer is scheduled to the UI thread, so an eventual result still cannot
   reliably reach a terminal projected navigation state.
6. The unresolved-symbol path returns an empty result and its truthful
   `No definition found.` feedback, matching A3; that does not prove the
   known-symbol Ready/auto-navigation path.

### `A1-FN-12` / BL-04 - document symbols

1. `editor.documentSymbol` reaches
   `EditorLanguageInputViewModel.RequestDocumentSymbols` and
   `LanguageSymbolService.RequestDocumentSymbols`.
2. The service validates active-document identity/version, publishes `Loading`,
   and calls `CsharpLsSession.RequestDocumentSymbolsAsync`.
3. After `ConfigureAwait(false)`, parsed symbols are bound to the current
   document URI, flattened, and synchronously published.
4. `EditorView.ApplySymbolSnapshot` mutates the document-symbol picker on the
   worker thread. The resulting observer exception is caught by the symbol
   request's broad exception handler and converted to the truthful
   `Document symbols failed.` state, explaining A3's `Failed`/zero-symbol
   result despite a Ready server capability.

### `A1-FN-13` / BL-05 - workspace symbols

1. `workbench.symbol` reaches
   `EditorLanguageInputViewModel.OpenWorkspaceSymbols` and
   `LanguageSymbolService.RequestWorkspaceSymbols`.
2. The initial surface opens on the caller thread. Query replacement is
   debounced with `ConfigureAwait(false)`; the request then calls
   `CsharpLsSession.RequestWorkspaceSymbolsAsync`.
3. After the LSP await, ordered navigable results are synchronously published
   on a worker thread.
4. `EditorView.ApplySymbolSnapshot` mutates the workspace-symbol picker without
   a UI scheduler. The observer exception is caught and converted to
   `Workspace symbols failed.`, explaining the A3 Failed state and missing
   cross-file results. The same defect applies to the zero-result terminal
   projection.

## Preserved Contracts

The following guards are live and must remain covered during M1-M3:

- `LanguageActiveDocumentValidator` checks active document identity, ready
  session generation, and synchronized document version.
- Completion, hover, definition, and document symbols reject ineligible or
  unsynchronized paths and discard stale version/generation results.
- Workspace symbols require a Ready session generation but intentionally do not
  require an active document.
- Request cancellation and replacement are owned independently by each service.
- Definition and symbol locations are validated before editor navigation;
  unresolved/empty, invalid, unsupported, cancelled, stale, and failed outcomes
  must remain truthful.
- Diagnostics, formatting, format-on-save, dirty state, undo, caret, and
  selection are preservation scope, not corrective targets.

## Focused Test Inventory

The filters below were checked with `--no-build --list-tests` against the
existing `tests/Zaide.Tests/bin/Debug/net10.0/Zaide.Tests.dll` at the baseline.

| Gate | Current files | Selected tests |
|------|---------------|---------------:|
| M0 DI | `tests/Zaide.Tests/App/Composition/LanguageRegistrationModuleTests.cs`; `tests/Zaide.Tests/Features/Language/DI/LanguageSessionServiceDiTests.cs` | 11 |
| M1 completion/routing | `tests/Zaide.Tests/Features/Language/Application/LanguageCompletionTests.cs`; `tests/Zaide.Tests/Features/Editor/Presentation/EditorLanguageInputRoutingTests.cs` | 16 |
| M2 hover/routing | `tests/Zaide.Tests/Features/Language/Application/LanguageHoverTests.cs`; `tests/Zaide.Tests/Features/Editor/Presentation/EditorLanguageInputRoutingTests.cs` | 12 |
| M3 definition/symbol/routing | `tests/Zaide.Tests/Features/Language/Application/LanguageNavigationTests.cs`; `tests/Zaide.Tests/Features/Language/Application/LanguageSymbolTests.cs`; `tests/Zaide.Tests/Features/Editor/Presentation/EditorLanguageInputRoutingTests.cs` | 37 |
| Preservation | `LanguageCommandAvailabilityTests.cs`; `LanguageDocumentSyncTests.cs`; `LanguageDiagnosticsServiceTests.cs`; `LanguageFormattingTests.cs` under `tests/Zaide.Tests/Features/Language/Application/` | 56 |

The current service tests use fake sessions and do not instantiate the live
`EditorView` projection under an Avalonia UI scheduler. The registration tests
replace the production scheduler with `CurrentThreadScheduler`. Those facts
explain why the focused suite protects service contracts but did not catch the
product-runtime thread-affinity failures. Later implementation milestones must
add focused regression coverage for their UI-dispatch boundary; M0 does not add
or change tests.

## Smoke and Harness Inventory

- `tools/Phase10M4CompletionHoverSmoke/` is retained and exercises the real
  csharp-ls completion/hover service pipeline, but it does not compose
  `EditorView` and is not A3 closeout evidence.
- `tools/Phase10M5NavigationSymbolsSmoke/` is retained and exercises the real
  csharp-ls definition/symbol service pipeline, but it also omits the editor UI
  projection and is not A3 closeout evidence.
- The A3 runner was intentionally out-of-tree at `/tmp/zaide-a3-lang/` and was
  deleted after evidence capture. There is no current in-repository A3 producer
  to invoke. M4 must recreate that runner at the locked path and use the exact
  command recorded in the implementation plan; source/service tests or the
  Phase 10 tools cannot substitute for it.

No candidate production class name was stale. M0 corrected the implicit
verification assumption that a retained A3 language-intelligence producer
exists. The exact live composition paths are under
`src/App/Composition/Registration/`, and the current parser names are the four
`LanguageServer*Parser` types listed above.

## Milestone and Rollback Decision

M1-M3 remain separate. They share an unscheduled synchronous-observer family,
but their failure points differ: completion fails after a terminal response,
hover can fail before its request starts, definition also has an unbounded
known-symbol request, and definition/symbols add auto-navigation, query
debounce, location validation, and picker ownership. That is not enough
evidence to merge their review, regression, or rollback boundaries.

Each milestone must be one independently revertible commit unless its accepted
implementation plan records a newly proven cross-cutting requirement. No
milestone may change the global scheduler registration, LSP request shapes,
parsers, provider policy, packages, or unrelated language features without new
evidence and a renewed human scope decision.
