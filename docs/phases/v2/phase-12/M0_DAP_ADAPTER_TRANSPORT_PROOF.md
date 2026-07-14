# Phase 12 M0: DAP Adapter and Transport Proof

## Purpose and result

This is a live, disposable Linux proof that the selected DAP adapter can speak
the transport and minimum lifecycle Phase 12 requires. It is not production
code and did not modify `src/`, `tests/`, packages, DI, settings, or the
repository's adapter installation state.

**Result: PASS.** NetCoreDbg 3.2.0-1092 launched with
`--interpreter=vscode` accepted Content-Length-framed DAP over stdio, launched
a real net10.0 C# DLL, emitted entry and breakpoint stops, answered thread,
stack, and scope requests, continued, and disconnected successfully.

## Environment and adapter provenance

| Item | Observed value |
|---|---|
| Date | 2026-07-14 |
| Host | Linux `arch`, x64 |
| SDK | .NET SDK 10.0.109; runtime 10.0.9 |
| Adapter | NetCoreDbg 3.2.0-1092 release archive, reported `NET Core debugger 3.2.0-1 (9744e1f, Release)` |
| Archive origin | `https://github.com/Samsung/netcoredbg/releases/download/3.2.0-1092/netcoredbg-linux-amd64.tar.gz` |
| Temporary extraction | `/tmp/zaide-phase12-m0-netcoredbg/netcoredbg/netcoredbg` |
| Adapter argv | `netcoredbg --interpreter=vscode` |
| Fixture project | `tests/fixtures/workflow-console/WorkflowConsole.csproj` |
| Fixture source | `tests/fixtures/workflow-console/Program.cs`, line 1 |
| Target program | absolute `tests/fixtures/workflow-console/bin/Debug/net10.0/WorkflowConsole.dll` |
| Target working directory | `tests/fixtures/workflow-console/` |

The release was downloaded to `/tmp` after confirming no `netcoredbg` or
`vsdbg` executable was installed and no DAP/debug-adapter NuGet package was in
the local package cache. It remains external proof material only; Phase 12 M0
does not install or vendor it.

## Live checkout seams proved relevant

| Live seam | Path | M0 finding |
|---|---|---|
| Existing framed RPC session | `src/Services/CsharpLsSession.cs` | Starts a child with redirected stdin/stdout/stderr and constructs `HeaderDelimitedMessageHandler` + `JsonRpc` with a camel-case `SystemTextJsonFormatter`. It is the transport precedent, not a debugger session to reuse directly. |
| RPC package | `src/Zaide.csproj` and central package props | `StreamJsonRpc` is already present at 2.22.23; M1 need not introduce a competing transport library. |
| Project target truth | `src/Services/IProjectContextService.cs`, `ProjectContext.cs`, `ProjectTargetResolver.cs` | Selected `CSharpProject` is the supported default debug source. `Workspace.WorkspacePath` remains forbidden as a target fallback. |
| Build/process owner | `src/Services/IProjectWorkflowService.cs`, `IManagedProcessRunner.cs` | Phase 11 owns redirected Build/Run/Test processes and one-operation policy; DAP needs its own adapter-session owner, not a PTY or a second target-discovery model. |
| Commands and UI host | `src/Services/ICommandRegistry.cs`, `src/ViewModels/MainWindowViewModel.cs` | Commands belong on the registry. Existing bottom panel owns Terminal, Problems, Output, and Test Results only; Debug surfaces must be explicitly added later. |
| App shutdown | `src/App.axaml.cs` | Explicit lifecycle ordering already exists, so M1 must insert debug teardown before workflow/language/project-context teardown. |

## Procedure

1. Obtained the official GitHub release asset above and extracted it only under
   `/tmp/zaide-phase12-m0-netcoredbg`.
2. Ran `netcoredbg --version`; it reported the version listed above.
3. Built the existing fixture with:

   ```bash
   dotnet build tests/fixtures/workflow-console/WorkflowConsole.csproj --nologo
   ```

   Result: `0 Warning(s)`, `0 Error(s)`; produced the absolute target DLL
   recorded above.
4. Spawned the adapter with redirected stdio. A disposable client wrote UTF-8
   JSON DAP requests framed as `Content-Length: <byte-count>\r\n\r\n<body>`
   and parsed adapter frames with the same byte-count rule.
5. Sent requests in this exact order:

   ```text
   initialize(clientID, adapterID=coreclr, pathFormat=path,
              linesStartAt1=true, columnsStartAt1=true)
   launch(program=<absolute DLL>, cwd=<fixture directory>,
          stopAtEntry=true, console=internalConsole)
   setBreakpoints(source=<absolute Program.cs>, breakpoints=[line 1])
   configurationDone
   threads
   stackTrace(threadId)
   scopes(frameId)
   continue(threadId)
   disconnect(restart=false, terminateDebuggee=true)
   ```

6. Drained stderr separately. No stderr text was needed as protocol input.

## Observed protocol evidence

| Exchange | Observed result | Meaning |
|---|---|---|
| `initialize` response | `supportsConfigurationDoneRequest=true` | Adapter accepted the client and requires the standard configuration boundary. |
| `launch` response | successful | Adapter accepted an absolute DLL and project working directory. |
| `setBreakpoints` response | `verified=false` before execution | Requested breakpoint was retained as pending/unverified; client must project this truthfully, not show it as hit/verified. |
| `configurationDone` | successful | Adapter accepted initial configuration after breakpoint submission. |
| `stopped` event | `reason=entry`, one thread | Initial stop events arrive asynchronously and must drive session state. |
| `threads` | count `1` | Thread request works in stopped state. |
| `stackTrace` | frames `1` | Stack retrieval works for returned thread. |
| `scopes` | count `1` | Scope retrieval works for returned top frame. |
| later `stopped` event | `reason=breakpoint` | The requested line-1 breakpoint was reached after the initially pending response. |
| `continue` + `disconnect` | successful; client printed `DAP lifecycle proof PASS` | Continue and terminating disconnect complete the lifecycle. |

The observed pending-then-hit breakpoint is intentional M0 evidence for the
M2/M3 requirement: persist the requested breakpoint separately from adapter
verification, and update visible state from `setBreakpoints` replies/events.

## Conclusions locked for implementation

1. NetCoreDbg is a viable Linux x64 C# DAP adapter for Phase 12's first launch
   path. M1 still owns a locator and truthful unavailable state; this proof does
   not authorize bundling or automatic download.
2. Existing `StreamJsonRpc` framing is compatible in principle with the
   adapter's stdio DAP transport. M1 must build a dedicated debug session and
   preserve DAP request/event ordering; it must not couple to the LSP session's
   methods or lifecycle.
3. `setBreakpoints` must occur before `configurationDone`; a false `verified`
   response does not mean the user breakpoint should be deleted.
4. Stack/scopes are stopped-state data and must be cleared/invalidated on
   continued, terminated, disconnect, adapter exit, and generation replacement.
5. The fixture can be reused for future recorded Linux smoke, but each later
   lifecycle/transport change needs fresh proof against the chosen adapter.

## M0 acceptance checklist

- [x] Adapter executable/version/provenance recorded
- [x] Real C# DLL built and launched
- [x] Content-Length stdio DAP framing exercised
- [x] Initialize/configuration lifecycle exercised
- [x] Breakpoint request and stopped-at-breakpoint event exercised
- [x] Thread, stack, and scope request paths exercised
- [x] Continue and terminating disconnect exercised
- [x] Existing production ownership seams recorded
- [x] No Phase 12 production code or repository-installed adapter
