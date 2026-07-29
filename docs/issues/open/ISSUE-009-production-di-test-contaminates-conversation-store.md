# ISSUE-009: Production DI test contaminates persisted conversation drafts

**Label:** BUG
**Status:** open
**Priority:** high
**Related:** test isolation, conversation persistence, Townhall, user data

## Description

When the user selects a direct agent conversation, the draft shows
`m6g-townhall-di-singleton-sync`. This is not a production placeholder or DI
registration key. It is a literal marker from
`TownhallRegistrationModuleTests.ProgramConfigureServices_ResolvesTownhallServicesAsSingletons`
that was written into the user's production conversation store.

## Steps to Reproduce

1. Run the production-composition singleton test without an isolated
   conversation-store path.
2. Allow the production service provider to dispose.
3. Launch the application.
4. Select the affected direct agent conversation.

**Expected behavior:** Automated tests must never read or write a user's
production conversation data.
**Actual behavior:** The test mutates `TownhallViewModel.DraftText` through a
production-composed provider, and disposal flushes the marker into the
production conversation snapshot. Zaide later restores it as a real draft.

## Debug Log

> If the fix is not obvious in 2 attempts, record everything here.

### Attempt 1
- **Hypothesis:** The marker is test data persisted through a production
  composition path.
- **Action:** Search for the exact marker, trace the test's service provider,
  and inspect conversation persistence ownership and the current user store.
- **Result:** The marker originates only in
  `tests/Zaide.Tests/App/Composition/TownhallRegistrationModuleTests.cs`.
  `Program.ConfigureServices` registers `ConversationPersistenceService`
  against production paths. The test changes the singleton Townhall draft,
  and provider disposal flushes pending state. The marker was present in both
  the current conversation snapshot and its last-known-good copy.
- **Error / Output:** Confirmed test isolation failure and user-data
  contamination.

### Attempt 2
- **Hypothesis:**
- **Action:**
- **Result:**
- **Error / Output:**

## Resolution

- **Root cause:** The production DI resolution test uses the real conversation
  persistence paths while mutating persisted presentation state.
- **Fix:**
- **Commit:**
- **Closed date:**
