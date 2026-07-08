# Phase 6 Smoke Test TOFIX

## Issue: Agent Panel Tab Strip Not Scrollable

During Phase 6 smoke testing, the agent panel host’s tab strip (`AgentPanelHostView`) was found to be non-scrollable when many agent panels are open. The editor tab bar (`EditorTabBar`) already used a working horizontal-scroll mechanism, but the agent panel was using a plain `StackPanel` wrapped in a horizontal container, which grants unlimited width and defeats scrolling.

## Root Cause

The first fix attempt wrapped the `_tabsPanel` in a `ScrollViewer` but **still placed that `ScrollViewer` inside a horizontal `StackPanel` (`leftStrip`)**. Avalonia gives horizontal `StackPanel` children unlimited width, so the `ScrollViewer` never received a constrained width to clip against — therefore no scrollbar appeared and the wheel handler had no effect.

## Fix Applied

1. Removed the horizontal `StackPanel` (`leftStrip`) wrapper completely.
2. Placed the `ScrollViewer` **directly in the `stripGrid`'s `Star` column** (same flat Grid placement used by `EditorTabBar`).
3. Added `VerticalAlignment = Center` on the `ScrollViewer` so tabs align vertically within the strip.
4. The `PointerWheelChanged` handler (vertical wheel → horizontal scroll, `delta * 50`, `e.Handled = true`) remains identical to `EditorTabBar`.

This change only affects the view layer; no ViewModel or test contract was altered.

## Verification

- `dotnet build Zaide.slnx --no-restore` → 0 warnings, 0 errors
- `dotnet test Zaide.slnx --no-build` → 721 passed, 0 failed

## Status

- [x] Fix implemented and verified by build/test
- [ ] Manual smoke test pending (visual confirmation of scrolling behavior)

---

## Issue: Townhall "All" Window Does Not Update Live on Agent-Panel Send

During Phase 6 smoke testing, typing and sending a message from an agent panel did not
refresh the Townhall chat window (the center-column `All`/`Chat`/`Activity` panel) until the
user switched a tab or filter. The mirrored activity was present in state, but the view only
repainted after a channel switch or filter change.

### Root Cause

`TownhallViewModel.FilteredMessages` drives the chat panel repaint via `messagesContentChanged`,
which was built from `propertyChanged.Where(name == nameof(Messages))`. That stream only emits
when the **`Messages` reference** changes (a channel switch). It never seeded the *initial*
active channel's collection, because `InitializeSessionState()` sets `Messages` to
`ChannelMessages[activeChannel]` **before** the observable is constructed — so that first
`PropertyChanged("Messages")` event is lost and no `CollectionChanged` subscription is ever
attached to the initial collection.

Consequence: agent-panel sends mirror into Townhall through
`MainWindowViewModel.SendAgentMessageAsync` → `AddMirroredActivity` → `LogActivity`, which adds
to the active collection and raises `CollectionChanged`. With no live subscription, `FilteredMessages`
never re-emits, `TownhallView.SetMessages` is never called, and the panel stays stale until the
user switches a tab/filter (which re-triggers `ApplyFilter()`).

### Fix Applied

In `src/ViewModels/TownhallViewModel.cs`, seeded `messagesContentChanged` with the current
`Messages` collection using a lazily-evaluated `Observable.Defer(() => Observable.Return(Messages …))`
merged with the ongoing reference-change stream:

```csharp
var messagesSeed = Observable.Defer(() => Observable.Return(Messages ?? new ObservableCollection<TownhallMessage>()));
var messagesRefChanged = propertyChanged
    .Where(name => name == nameof(Messages))
    .Select(_ => Messages ?? new ObservableCollection<TownhallMessage>());
var messagesContentChanged = Observable.Merge(messagesSeed, messagesRefChanged)
    .DistinctUntilChanged()
    .Select(m => Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
            h => m.CollectionChanged += h,
            h => m.CollectionChanged -= h)
        .Select(_ => Unit.Default)
        .StartWith(Unit.Default))
    .Switch();
```

The `Defer` is essential: a plain eager seed would capture the construction-time channel, which
breaks once the user switches channels before subscribing. `Defer` re-evaluates `Messages` at
subscribe time so it always seeds the *currently active* collection. The existing
`DistinctUntilChanged()` + `Switch()` keep the single-live-subscription (no stale-leak) guarantee.

Added a canary test `MirroredActivity_UpdatesFilteredMessages_WithoutTabOrFilterChange` in
`tests/Zaide.Tests/ViewModels/TownhallViewModelTests.cs` that mirrors an activity with no channel
switch or filter change and asserts `FilteredMessages` updates.

### Verification

- `dotnet build Zaide.slnx --no-restore` → 0 warnings, 0 errors
- `dotnet test Zaide.slnx --no-build` → 722 passed, 0 failed (was 721 before the added canary test)

### Status

- [x] Fix implemented and verified by build/test
- [x] Covered by `MirroredActivity_UpdatesFilteredMessages_WithoutTabOrFilterChange`
- [ ] Manual smoke test pending (visual confirmation of live Townhall refresh on agent send)
</tool_call>