# Refactor 3 Implementation TODO (blackboxai/refactor-3-agent-first-layout)

## Milestones
- [ ] M1 Shell layout transition
  - [ ] Add `src/Views/NavBar.cs`
  - [ ] Add `src/Views/SourceControlPlaceholder.cs`
  - [ ] Update `src/ViewModels/MainWindowViewModel.cs` with left-panel mode state
  - [ ] Refactor `src/MainWindow.axaml.cs` to nav | left panel | townhall | editor
  - [ ] Bottom panel spans center+right only

- [x] M2 Townhall models/viewmodel
  - [x] Add `src/Models/Channel.cs`
  - [x] Add `src/Models/TownhallMessage.cs`
  - [x] Add `src/Models/WorkspaceAgent.cs`
  - [x] Add `src/Models/TownhallState.cs`
  - [x] Add `src/ViewModels/TownhallViewModel.cs`
  - [ ] Wire `TownhallViewModel` into `MainWindowViewModel`

- [ ] M3 Townhall views
  - [x] Add `src/Views/TownhallView.cs`
  - [x] Add `src/Views/TownhallChannelPanel.cs`
  - [x] Add `src/Views/TownhallChatPanel.cs`
  - [x] Add `src/Views/TownhallPeoplePanel.cs`
  - [x] Add `src/Views/TownhallInputArea.cs`
  - [ ] Wire Townhall view into center column in `MainWindow.axaml.cs`

- [ ] M4 Editor adaptation
  - [ ] Update `src/Views/EditorTabBar.cs` with “Shared in #townhall” indicator visibility rules
  - [ ] Keep editor code-surface readability unchanged, quieter auxiliary styling

- [ ] M5 Terminal/log alignment + label
  - [ ] Update `src/Views/TerminalPanel.cs` header label to “TERMINAL / LOGS”
  - [ ] Confirm bottom panel layout contract in `MainWindow.axaml.cs`

- [ ] M0.5 Palette/token cleanup in touched files
  - [ ] Replace hardcoded colors with App resource tokens where applicable

- [ ] Validation
  - [ ] dotnet build Zaide.slnx
  - [ ] dotnet test Zaide.slnx --no-build
