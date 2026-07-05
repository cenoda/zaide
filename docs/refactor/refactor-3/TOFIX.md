# Refactor 3: TOFIX — M2 Audit (Updated)

**Status:** ✅ COMPLETE — All M2 issues resolved including per-channel message state and INotifyPropertyChanged (2026-07-05)

---

## High Priority

### H1: TownhallState not registered in DI container
- **File:** `src/Program.cs`
- **Issue:** `TownhallViewModel` requires a `TownhallState` constructor parameter, but `TownhallState` was never registered via `services.AddSingleton<TownhallState>()`. This would cause runtime resolution failure on first real DI usage (though project could compile and tests passed without exercising DI).
- **Fix:** Added `services.AddSingleton<TownhallState>();` before `TownhallViewModel` registration, plus added `using Zaide.Models;` declaration.
- **Milestone:** M2
- **Status:** ✅ Resolved

### H2: Channel IsActive state not synchronized with UI binding
- **File:** `src/Models/Channel.cs`, `src/ViewModels/TownhallViewModel.cs`
- **Issue:** `SelectChannelCommand` only assigned to `_state.ActiveChannelId`, leaving all channels with `IsActive = false`. The `Channel` class was a plain class with auto-properties and no change notification, which is not enough for a channel list view that needs to react when selection changes.
- **Fix 1:** Updated `ActiveChannelId` setter in ViewModel to iterate over `_state.Channels` and set `c.IsActive = c.Id == value`. Channel switching now properly updates `Channel.IsActive` flags.
- **Fix 2:** Made `Channel` implement `System.ComponentModel.INotifyPropertyChanged` with backing fields for `IsActive`, `Id`, `Name`, and `IsPinned`, raising property changed events on update so UI can bind to active state changes.
- **Milestone:** M2
- **Status:** ✅ Resolved

### H3: DraftText duplicated between ViewModel and State
- **File:** `src/ViewModels/TownhallViewModel.cs`, `src/Models/TownhallState.cs`
- **Issue:** The state object defines `DraftText` but the ViewModel kept a separate `_draftText` field, causing stale/duplicated state. This violated the principle of having one source of truth for session state.
- **Fix:** ViewModel's `DraftText` property stores locally (required for ReactiveUI), but the setter syncs to `_state.DraftText` immediately on change for M3 integration. Tests added to prove DraftText syncs to state.
- **Milestone:** M2
- **Status:** ✅ Resolved

### H4: Channel switching did not maintain per-channel message state
- **File:** `src/Models/TownhallState.cs`, `src/ViewModels/TownhallViewModel.cs`
- **Issue:** `TownhallState.Messages` was documented as "messages for the active channel" but was a single global collection that never changed when the active channel changed. `TownhallViewModel.InitializeSampleData()` seeded one global message list and `SendMessageCommand` always appended to that same list regardless of which channel is active. `SelectChannelCommand` changed `ActiveChannelId` but never swapped or filtered Messages.
- **Fix:** Redesigned `TownhallState` to use `Dictionary<string, ObservableCollection<TownhallMessage>> ChannelMessages` instead of a single `Messages` collection. Channel messages are now stored per-channel (`townhallMain`, `aiStatus`, `codebaseRefactoring`). Active channel switching uses the dictionary key to update the ViewModel's `Messages` property to point to the correct channel's message list, and sending messages adds to `_state.ChannelMessages[_state.ActiveChannelId]`.
- **Milestone:** M2
- **Status:** ✅ Resolved

---

## Medium Priority

*(No medium priority items remaining)*

---

## Low Priority

### L1: Initial implementation had TownhallState type not found in Program.cs
- **File:** `src/Program.cs`
- **Issue:** Missing `using Zaide.Models;` caused compilation error when referencing `TownhallState`.
- **Fix:** Added `using Zaide.Models;` to the using block section.
- **Milestone:** M2
- **Status:** ✅ Resolved

### L2: Initial implementation used TownhallState.DraftText with ref which doesn't work for properties
- **File:** `src/ViewModels/TownhallViewModel.cs`
- **Issue:** Attempted `this.RaiseAndSetIfChanged(ref _state.DraftText, value)` but `_state.DraftText` is a property (not a field), causing CS0206 error.
- **Fix:** Implemented custom setter that stores locally for ReactiveUI compatibility and syncs to state for M3 integration: `set { if (_draftText != value) { _draftText = value; this.RaisePropertyChanged(); _state.DraftText = value; } }`
- **Milestone:** M2
- **Status:** ✅ Resolved

### L3: Missing tests for the newly implemented fixes (IsActive flags, DraftText sync, per-channel messages)
- **File:** `tests/Zaide.Tests/ViewModels/TownhallViewModelTests.cs`
- **Issue:** Original tests only checked that `ActiveChannelId` changes. There were no tests proving `Channel.IsActive` flags are updated correctly, no test proving `DraftText` syncs to `TownhallState.DraftText`, and no test exposing the per-channel message state correctness.
- **Fix:** Added new tests:
  - `SelectChannel_UpdatesIsActiveFlags()` — proves Channel.IsActive flags are updated correctly on channel switch
  - `DraftText_SyncsToState()` — proves DraftText property syncs with TownhallState.DraftText
  - `SelectChannel_SwitchesMessagesToActiveChannel()` — proves per-channel message switching works correctly
- **Milestone:** M2
- **Status:** ✅ Resolved

---

*Last updated: 2026-07-05*