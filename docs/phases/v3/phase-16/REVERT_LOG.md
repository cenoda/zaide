# Phase 16: Qwen Observational Path Revert Log

## What Was Reverted

- **Reverted from:** `992380e4` (last Qwen observational-path commit).
- **Reverted to:** `52fbe57e` (last known-good Phase 16 baseline before the Qwen amendment).
- **Commits reverted:** `63ac05bb..992380e4` (28 commits), comprising the Qwen Code + DeepSeek single-candidate amendment, M3 acquisition/egress/DNS evidence, qualification harness, TC-T01 fixture, policy remediations, and qualification records.
- **Files removed:** the Qwen-specific M3 qualification harness, TC-T01 fixture, M3-only tests, and M3 acquisition/qualification/remediation evidence records.

The Phase 16 M0/M2 baseline remains. This revert does not alter production application code.

## Root Cause

The Qwen Code observational path was selected as the only then-eligible candidate for a constrained single-candidate experiment; it was not selected based on a quality claim.

Repeated authorized qualification sessions verified the TC-T01 workspace mutation, but Qwen did not satisfy the required clean process completion contract (`qwen_exit=0`). The final explicitly approved extended smoke also exhausted its 240-turn / 800-second exception without a clean exit. Further ceiling increases would not provide a proportionate or controlled qualification signal.

The path is therefore reverted rather than patched forward. No comparative or quality conclusion is retained.

## Rules Added

No repository-wide rule was added. A future Phase 16 candidate path must start from a new human-approved amendment and independently establish its qualification contract, evidence boundaries, and cost authorization.

## Revert Commit

Recorded by the commit that adds this revert log.
