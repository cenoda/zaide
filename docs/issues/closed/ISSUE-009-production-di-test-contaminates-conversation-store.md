# ISSUE-009: Production DI test contaminates persisted conversation drafts

**Label:** BUG  
**Status:** closed  
**Priority:** high  
**Related:** test isolation, conversation persistence, Townhall, user data,
Phase 23 F14

## Description

When the user selected a direct agent conversation or channel, the composer
draft showed `m6g-townhall-di-singleton-sync`. This is not a production
placeholder or DI registration key. It is a literal marker from
`TownhallRegistrationModuleTests.ProgramConfigureServices_ResolvesTownhallServicesAsSingletons`
that was written into the user's production conversation store.

Real product placeholders remain: `Message...` / `Message #…` /
`Direct message with …`.

## Steps to Reproduce (pre-fix)

1. Run the production-composition singleton test without an isolated
   conversation-store path.
2. Allow the production service provider to dispose.
3. Launch the application.
4. Select the affected direct agent conversation or channel.

**Expected behavior:** Automated tests must never read or write a user's
production conversation data.  
**Actual behavior:** The test mutated `TownhallViewModel.DraftText` through a
production-composed provider, and disposal flushed the marker into the
production conversation snapshot. Zaide later restored it as a real draft.

## Debug Log

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

- **Hypothesis:** Removing the production `DraftText` mutation is sufficient;
  singleton identity can be proven with `Assert.Same` only. Draft sync remains
  covered by temp-path unit tests elsewhere.
- **Action:** Stop assigning any marker/`DraftText` on the production-composed
  provider; keep `Assert.Same` for state/VM/collection/persistence singletons;
  add a source guard that rejects reintroduction of the marker and of
  `DraftText` assignment inside the production resolve test. Scrub exact marker
  values from this machine's store (backup first; delete draft keys only).
- **Result:** Suite can re-run without re-polluting production drafts; store
  marker absent after scrub.

## Resolution

- **Root cause:** The production DI resolution test used the real conversation
  persistence paths while mutating persisted presentation state
  (`DraftText` set to the test marker string). On `ServiceProvider` dispose,
  `ConversationPersistenceService` flushed the pending snapshot into
  `~/.config/zaide/conversations/conversations.json` (and last-known-good).
- **Fix:**
  1. **Test isolation:** `ProgramConfigureServices_ResolvesTownhallServicesAsSingletons`
     no longer assigns `DraftText`. It proves singleton identity only
     (`TownhallState`, `TownhallViewModel`, shared Channels/Agents collections,
     `ConversationPersistenceService`).
  2. **Guard:** Source-level test rejects the contamination marker string and
     any `DraftText =` assignment inside that production resolve method body.
  3. **Data scrub (local machine, not in repo):** Backup then remove draft
     entries whose value equals the exact contamination marker from both live
     store files. Conversations, channels, agents, last-read maps, and
     `activeConversationId` were left untouched.
- **Commit:** master — `fix(phase-23): isolate Townhall DI singleton test from production conversation store (ISSUE-009)`
- **Closed date:** 2026-08-05

## Manual verification

1. `dotnet build Zaide.slnx`
2. Interactive: `dotnet test Zaide.slnx --no-build`
3. After tests: marker must not appear in live
   `~/.config/zaide/conversations/conversations.json` or
   `conversations.json.lastknowngood` (backups may still contain it).
4. Launch Zaide (or reselect conversation if already running) → composer must
   not show the marker; placeholders are normal product copy.

## Not changed

- Production DI registration of `ConversationPersistenceService` (still real
  paths at runtime — correct for the app).
- Product placeholder strings.
- F1 / F3 / F5 / F6 UI work (separate Phase 23 findings).
- No broad DI rewrite; no user conversation history deleted.
