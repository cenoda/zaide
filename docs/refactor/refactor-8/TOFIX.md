# Refactor 8: Review Findings

## Resolved

- [x] **R8-M1-001 — DAP disposal race blocked the required full-suite gate.**
  The post-M1 full-suite run exposed `DapContentLengthTransportTests.
  DisposeRacingOutstandingRequests_CancelsAllWithoutCollectionExceptions` as a
  timing-sensitive test. Tightening the test to dispose while requests were
  still admitted exposed the underlying lifecycle defect: `DisposeAsync` could
  dispose `_writeGate` while an admitted request still needed to release it,
  causing `ObjectDisposedException`. The transport now cancels and drains
  admitted requests before disposing the write gate; the test verifies the
  pending set directly instead of polling outbound frames. This was an
  explicitly user-authorized gate repair, not Refactor 8 UI scope expansion.
