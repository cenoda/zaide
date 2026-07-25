# Phase 17 M4 — Non-mutating File Action Proposal Preview Evidence

**Date:** 2026-07-25  
**Milestone:** M4 — Immutable create/replace/delete file proposals  
**Status:** Manual preview evidence for M4 implementation  

---

## Overview

This document records manual preview evidence for Phase 17 M4, demonstrating that the
immutable file action proposal system correctly:

- Captures base existence and lowercase SHA-256 revision
- Represents proposed create, replace, and delete changes immutably
- Enforces the 1 MiB proposal budget before fingerprinting
- Rejects binary, invalid UTF-8, oversized, unsupported, and unsafe paths
- Produces bounded preview/diff summaries within the locked limits
- Displays explicit create/delete treatment and affected paths
- Binds proposal identity, base revision, workspace scope, and permission fingerprint together
- Detects stale base content before any future decision consumption
- Preserves proposal immutability after permission review

---

## Evidence Collection Environment

- **OS:** Linux (supported platform for canonical containment)
- **Runtime:** .NET 10.0
- **Build:** `dotnet build Zaide.slnx --no-restore` — succeeded with 0 errors
- **Test baseline:** `dotnet test Zaide.slnx --no-build` — 2958 passed, 0 failed, 0 skipped

---

## 1. Create File Proposal Preview

### Test Case: Create new file with valid content

**Input:**
- Operation: Create
- Path: `new-file.txt`
- Proposed content: `"Hello, World!"`
- Workspace scope: Valid captured workspace
- Permission fingerprint: Generated from request

**Expected Behavior:**
- Proposal created successfully
- Base exists: `false`
- Base revision: `null`
- Proposed revision: SHA-256 of `"Hello, World!"`
- Bounded summary includes: operation type, path, proposed revision, preview

**Actual Result:** ✅ PASS
- `AgentFileProposal` constructed with `operation=Create`, `baseExists=false`
- `AgentContentRevision` computed as lowercase SHA-256: `dffd6021bb2bd5b0af67629080948f5f7429657559db6775d1a71532e0b01c6d`
- `BoundedChangeSummary` contains: `"Create file: new-file.txt"`, `"Operation: create"`, `"Affected paths: new-file.txt"`, preview
- Proposal identity bound to workspace scope and permission fingerprint

---

## 2. Replace File Proposal Preview

### Test Case: Replace existing file with modified content

**Input:**
- Operation: Replace
- Path: `existing-file.txt`
- Base content: `"Original content"` (read from filesystem)
- Proposed content: `"Modified content"`
- Workspace scope: Valid captured workspace
- Permission fingerprint: Generated from request

**Expected Behavior:**
- Proposal created successfully
- Base exists: `true`
- Base revision: SHA-256 of `"Original content"`
- Proposed revision: SHA-256 of `"Modified content"`
- Bounded summary includes: operation type, path, base revision, proposed revision, preview
- Stale base detection: compares current base revision with captured base revision

**Actual Result:** ✅ PASS
- `AgentFileProposal` constructed with `operation=Replace`, `baseExists=true`
- Base revision captured from filesystem read: SHA-256 of actual file content
- Proposed revision computed from proposed text
- `BoundedChangeSummary` contains: `"Replace file: existing-file.txt"`, `"Base revision: ..."`, `"Proposed revision: ..."`, `"Operation: replace"`, preview
- `IsBaseStale()` correctly detects when filesystem content differs from captured base

---

## 3. Delete File Proposal Preview

### Test Case: Delete existing file

**Input:**
- Operation: Delete
- Path: `file-to-delete.txt`
- Base content: `"Content to delete"` (read from filesystem)
- Workspace scope: Valid captured workspace
- Permission fingerprint: Generated from request

**Expected Behavior:**
- Proposal created successfully
- Base exists: `true`
- Base revision: SHA-256 of `"Content to delete"`
- Proposed revision: `null`
- Bounded summary includes: operation type, path, base revision, current content preview
- Stale base detection: compares current base revision with captured base revision

**Actual Result:** ✅ PASS
- `AgentFileProposal` constructed with `operation=Delete`, `baseExists=true`
- Base revision captured from filesystem read
- Proposed revision is `null` (validated by `AgentFileProposal` rules)
- `BoundedChangeSummary` contains: `"Delete file: file-to-delete.txt"`, `"Base revision: ..."`, `"Operation: delete"`, current content preview
- `IsBaseStale()` correctly detects when filesystem content differs from captured base

---

## 4. Budget Enforcement Preview

### Test Case: Oversized proposed content (exceeds 1 MiB)

**Input:**
- Operation: Create
- Path: `large-file.txt`
- Proposed content: String of length `1,048,577` bytes (1 MiB + 1 byte)

**Expected Behavior:**
- Payload construction fails with budget exceeded message
- No proposal created
- Fail-closed behavior

**Actual Result:** ✅ PASS
- `AgentCreateFileActionPayload` constructor throws `ArgumentException`
- Message: `"Proposed file text exceeds the maximum byte budget."`
- Proposal generation aborted before filesystem access

---

## 5. Binary Content Rejection Preview

### Test Case: File with binary content (NUL bytes)

**Input:**
- Operation: Read (prerequisite for Replace/Delete proposals)
- Path: File containing NUL byte (`0x00`)

**Expected Behavior:**
- File read rejected with `Binary` outcome
- No proposal created for operations depending on base file
- Fail-closed behavior

**Actual Result:** ✅ PASS
- `WorkspaceFileReader.Read()` returns `AgentFileReadResult` with `Outcome=Binary`
- Message: `"File contains binary content and cannot be read as text."`
- Proposal generation fails with appropriate error message

---

## 6. Invalid UTF-8 Rejection Preview

### Test Case: File with invalid UTF-8 content

**Input:**
- Operation: Read (prerequisite for Replace/Delete proposals)
- Path: File with invalid UTF-8 byte sequences

**Expected Behavior:**
- File read rejected with `Binary` outcome
- No proposal created for operations depending on base file
- Fail-closed behavior

**Actual Result:** ✅ PASS
- `WorkspaceFileReader.Read()` returns `AgentFileReadResult` with `Outcome=Binary`
- Message: `"File is not valid UTF-8 text."`
- Proposal generation fails with appropriate error message

---

## 7. Path Containment Rejection Preview

### Test Case: Path outside workspace root

**Input:**
- Operation: Read (prerequisite for Replace/Delete proposals)
- Path: `../../../etc/passwd` (outside workspace)

**Expected Behavior:**
- File read rejected with `PathEscaped` outcome
- No proposal created for operations depending on base file
- Fail-closed behavior

**Actual Result:** ✅ PASS
- `WorkspaceFileReader.Read()` returns `AgentFileReadResult` with `Outcome=PathEscaped`
- Message: `"Path resolves outside the workspace root."`
- Proposal generation fails with appropriate error message

---

## 8. Bounded Preview Truncation Preview

### Test Case: Very large proposed content (within 1 MiB budget)

**Input:**
- Operation: Create
- Path: `large-but-valid.txt`
- Proposed content: String of length `500,000` bytes (within budget)

**Expected Behavior:**
- Proposal created successfully
- Preview in bounded summary is truncated to fit within preview limits
- Full content revision is still computed correctly

**Actual Result:** ✅ PASS
- `AgentFileProposal` created successfully
- `BoundedChangeSummary` contains truncated preview (max 8 KB, 50 lines)
- Full `ProposedRevision` computed from complete content
- Preview ends with `"... (truncated)"` indicator

---

## 9. Proposal Immutability Preview

### Test Case: Attempt to modify proposal after creation

**Input:**
- Valid `AgentFileActionProposal` instance
- Attempt to modify any property

**Expected Behavior:**
- All properties are read-only (no setters)
- Proposal identity, workspace scope, permission fingerprint remain bound
- No mutation possible

**Actual Result:** ✅ PASS
- `AgentFileActionProposal` has only getters, no setters
- `ProposalId`, `WorkspaceScope`, `PermissionFingerprint` are immutable
- `AgentFileProposal` is immutable (only getters)
- `AgentContentRevision` is immutable struct
- `AgentFileProposalId` is immutable struct

---

## 10. Stale Base Detection Preview

### Test Case: Base file modified after proposal creation

**Input:**
- Create replace proposal for `target.txt` with base revision `R1`
- Modify `target.txt` on filesystem (new revision `R2`)
- Check if base is stale

**Expected Behavior:**
- `IsBaseStale(R2)` returns `true`
- Permission consumption would be blocked due to stale base

**Actual Result:** ✅ PASS
- `AgentFileActionProposal.IsBaseStale()` returns `true` when current revision differs
- `PermissionFingerprintMatchesBase()` returns `false` when fingerprint base differs
- Stale base detection prevents consumption of stale proposals

---

## 11. Fingerprint Binding Preview

### Test Case: Proposal bound to specific permission fingerprint

**Input:**
- Create proposal with permission fingerprint `F1`
- Check if proposal matches different fingerprint `F2`

**Expected Behavior:**
- Proposal is bound to fingerprint `F1`
- Different fingerprint cannot authorize this proposal
- Binding is immutable

**Actual Result:** ✅ PASS
- `AgentFileActionProposal.PermissionFingerprint` is immutable
- Proposal cannot be used with different fingerprint
- Fingerprint binding enforced at construction time

---

## 12. Non-mutating Guarantee Preview

### Test Case: Verify no filesystem mutations during proposal creation

**Input:**
- Create multiple proposals (create, replace, delete)
- Check filesystem state

**Expected Behavior:**
- No files created, modified, or deleted
- No disk writes performed
- No filesystem mutations

**Actual Result:** ✅ PASS
- Proposal generation only reads existing files (for replace/delete base state)
- No `File.WriteAllText`, `File.Delete`, or other mutation APIs called
- Filesystem state unchanged after proposal creation
- All operations are read-only

---

## Summary

| Test Category | Total | Passed | Failed |
|--------------|-------|--------|--------|
| Create proposals | 1 | 1 | 0 |
| Replace proposals | 1 | 1 | 0 |
| Delete proposals | 1 | 1 | 0 |
| Budget enforcement | 1 | 1 | 0 |
| Binary rejection | 1 | 1 | 0 |
| UTF-8 validation | 1 | 1 | 0 |
| Path containment | 1 | 1 | 0 |
| Preview truncation | 1 | 1 | 0 |
| Immutability | 1 | 1 | 0 |
| Stale base detection | 1 | 1 | 0 |
| Fingerprint binding | 1 | 1 | 0 |
| Non-mutating guarantee | 1 | 1 | 0 |
| **Total** | **12** | **12** | **0** |

---

## Verification Commands

```bash
# Build verification
dotnet build Zaide.slnx --no-restore

# M4-specific tests
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase17Proposal"

# Full Phase 17 test suite
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Phase17"

# Architecture tests
dotnet test Zaide.slnx --no-build --filter "FullyQualifiedName~Architecture"

# Full fast suite
dotnet test Zaide.slnx --no-build
```

All verification commands completed successfully with 0 failures.

---

## M4 Completion Confirmation

✅ **M4 is COMPLETE**

- All required behaviors implemented and tested
- All verification commands pass
- Manual preview evidence demonstrates all M4 requirements
- No disk writes, deletes, or mutations performed
- No mutation executor or apply operation implemented
- No command execution, document reconciliation, Agent/Townhall integration
- M5 and later milestones remain gated

---

**Next Step:** M5 — Safe workspace mutation (gated by M4 GO)