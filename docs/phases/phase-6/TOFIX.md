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

---

## Issue: Agent Panel Input Not Cleared After Send (Duplicate Re-Send)

During Phase 6 smoke testing, sending a message from an agent panel worked, but the typed text
stayed in the input box. Typing `hi` and pressing Enter 3 times re-sent `hi` 3 times, because the
draft was never cleared on a failed request.

### Root Cause

`AgentExecutionCoordinator.SendAsync` cleared `panel.DraftInput` **only in the success branch**
(`if (result.IsSuccess) { panel.DraftInput = string.Empty; … }`). On any failure path — missing API
key, missing base URL/model, HTTP error, or exception — the draft was left intact. The input box is
two-way bound to `DraftInput` (`AgentPanelView` → `this.Bind(ViewModel, vm => vm.DraftInput, …)`),
so the text remained and the next Enter re-sent it. The existing test
`SendAsync_Failure_DoesNotClearDraftInput` even encoded this as intended behavior.

### Fix Applied

Moved the draft-clear to **send initiation**, before the execution call, so the input box empties
regardless of outcome:

```csharp
panel.OutputHistory.Add($"User: {userMessage}");

// Consume the draft immediately so the input box clears and the same
// text cannot be re-sent by pressing Enter again (e.g. when the
// request later fails). The user can always re-type to retry.
panel.DraftInput = string.Empty;

var result = await _executionService.ExecuteAsync(userMessage, ct).ConfigureAwait(false);
```

Updated tests in `tests/Zaide.Tests/ViewModels/AgentExecutionCoordinatorTests.cs`:
- `SendAsync_Failure_DoesNotClearDraftInput` → `SendAsync_Failure_ClearsDraftInput` (asserts draft is empty after failure).
- `SendAsync_MissingApiKey_AppendsErrorToOutput` now asserts the draft is cleared.
- Added regression test `SendAsync_RepeatedEnter_ClearsDraftEachTime_NoDuplicateSend` simulating
  typing `hi` and pressing Enter 3 times; asserts the draft is empty after each send and exactly
  one `User: hi` entry exists per Enter (no duplicates from a lingering draft).

### Verification

- `dotnet test Zaide.slnx --no-build` → 723 passed, 0 failed (was 722 before the added regression test)

### Status

- [x] Fix implemented and verified by build/test
- [x] Covered by `SendAsync_Failure_ClearsDraftInput` and `SendAsync_RepeatedEnter_ClearsDraftEachTime_NoDuplicateSend`
- [ ] Manual smoke test pending (visual confirmation input box empties on send, including failed send)

---

## Issue: Townhall Chat Enter Inserts Newline Instead of Sending

During Phase 6 smoke testing, typing in the Townhall chat input and pressing Enter behaved like
Shift+Enter — it only inserted a newline and never sent the message.

### Root Cause

`TownhallInputArea` constructed its `TextBox` with `AcceptsReturn = true`. In Avalonia, a control's
class handler for `KeyDown` runs **before** instance handlers registered via `+=`. With
`AcceptsReturn` true, the `TextBox` class handler consumes Enter (inserts a newline and marks the
`KeyDown` routed event `Handled = true`) before our instance handler — which calls `TriggerSend()` —
ever runs. So Enter produced a newline and the message was never sent, exactly mirroring Shift+Enter
behavior. (The headless unit tests passed because synthetic `RaiseEvent(KeyEventArgs)` bypasses the
`TextBox` class handler and reaches our instance handler directly.)

### Fix Applied

In `src/Views/TownhallInputArea.cs`:

1. Set `AcceptsReturn = false` so the `TextBox` never consumes Enter and our `KeyDown` handler always
   runs first.
2. Reworked the `KeyDown` handler to handle both cases explicitly:
   - Enter (no Shift) → `TriggerSend()` (send the message).
   - Shift+Enter → `InsertNewlineAtCaret()` (insert a newline at the caret position, since
     `AcceptsReturn` is now disabled).
   - Both branches set `e.Handled = true`.
3. Added the `InsertNewlineAtCaret()` helper, which inserts `Environment.NewLine` at
   `_inputField.CaretIndex` and repositions the caret after the inserted text.

Updated tests in `tests/Zaide.Tests/Views/TownhallInputAreaTests.cs`:
- `InputField_AcceptsReturn_IsTrue` → `InputField_AcceptsReturn_IsFalse` (asserts `AcceptsReturn` is now `false`).
- Added `ShiftEnterKey_InsertsNewlineAtCaret` asserting Shift+Enter inserts a newline at the caret
  and does not trigger a send.

### Verification

- `dotnet test Zaide.slnx --no-build` → 724 passed, 0 failed (was 723 before the added regression test)

### Status

- [x] Fix implemented and verified by build/test
- [x] Covered by `InputField_AcceptsReturn_IsFalse` and `ShiftEnterKey_InsertsNewlineAtCaret`
- [ ] Manual smoke test pending (visual confirmation Enter sends, Shift+Enter inserts newline)
