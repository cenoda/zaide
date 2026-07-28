# Phase 20 M1 — Schema Conformance Evidence

## Pinned artifact lock

| Artifact | Value |
|----------|-------|
| Wire protocol | ACP v1 (`initialize.protocolVersion = 1`) |
| Stable schema | `schema-v1.20.0` |
| Schema commit | `5e89c71497fe07dd4ae633c181a17224f4a8956d` |
| `schema.json` SHA-256 | `92c1dfcda10dd47e99127500a3763da2b471f9ac61e12b9bf0430c32cf953796` |
| `meta.json` SHA-256 | `e0bf36f8123b2544b499174197fdc371ec49a1b4572a35114513d56492741599` |

Frozen fixtures live at:

- `tests/Zaide.Tests/Features/Agents/Acp/Protocol/Fixtures/schema-v1.20.0.json`
- `tests/Zaide.Tests/Features/Agents/Acp/Protocol/Fixtures/meta-v1.20.0.json`

Tests verify digests and method names without live download.

## M1 implemented protocol subset

| Direction | Methods |
|-----------|---------|
| Client → agent | `initialize`, `session/new`, `session/prompt`, `session/cancel` |
| Agent → client | `session/update` (parse/all bounded variants) |
| Protocol | `$/cancel_request` (notification encode/decode) |

Deferred to later milestones: authentication invocation, client filesystem,
terminal lifecycle, session load/list/delete/resume/close, and production process
hosting.

## Production placement

- `src/Features/Agents/Infrastructure/Acp/` — JSON-RPC envelopes, wire DTOs,
  codec, newline framing, protocol session plumbing
- `src/Features/Agents/Domain/AcpSchemaProfile.cs` — Zaide-owned schema lock

No ACP SDK, no `System.Diagnostics.Process`, no production DI, and no Native
Harness references.

## Verification

```bash
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase20Protocol"
```

Gate must discover tests and pass with zero failures.
