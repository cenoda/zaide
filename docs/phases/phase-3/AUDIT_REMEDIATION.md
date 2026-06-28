# Phase 3 Audit Remediation Summary

This document summarizes the changes made to the Phase 3 Implementation Plan based on the audit findings.

## Critical Issues Fixed

### 1. NuGet Package Name Correction
**Issue:** `Mono.Posix` does not exist on NuGet.org
**Fix:** Changed all references from `Mono.Posix` to `Mono.Posix.NETStandard` v1.0.0

### 2. forkpty() P/Invoke Research
**Issue:** `forkpty()` may not be available in Mono.Posix.NETStandard
**Fix:** Added explicit mention of manual P/Invoke declaration if needed:
```csharp
[DllImport("libutil", SetLastError = true)]
private static extern int forkpty(out int master, IntPtr name, IntPtr termios, IntPtr winsize);
```

### 3. CancellationToken on ITerminalService.StartAsync
**Issue:** Missing CancellationToken parameter
**Fix:** Updated interface signature:
```csharp
Task StartAsync(string shell = "/bin/bash", CancellationToken ct = default);
```

### 4. SendInput Type Consistency
**Issue:** Inconsistency between ViewModel (string) and View (byte[])
**Fix:** Changed ViewModel to accept byte[]:
```csharp
SendInputAsync(byte[] data)
```

## Moderate Issues Fixed

### 5. Buffer Capacity Increase
**Issue:** 100,000 characters too small
**Fix:** Increased to 200,000 characters (configurable)

### 6. StatusText Integration
**Issue:** Not wired to MainWindowViewModel
**Fix:** Added subscription in MainWindowViewModel.Activate():
```csharp
_disposables.Add(
    this.WhenAnyValue(x => x.TerminalViewModel.StartupError)
        .Where(err => err is not null)
        .Subscribe(err => StatusText = $"Terminal: {err}"));
```

### 7. Input Focus Management
**Issue:** No auto-focus on terminal TextBox
**Fix:** Added requirement to auto-focus TextBox when panel becomes visible

### 8. Trim Strategy Specification
**Issue:** Trim strategy not precisely defined
**Fix:** Specified O(1) bulk remove strategy

### 9. Async Suffix Convention
**Issue:** SendInput should follow async naming convention
**Fix:** Changed to SendInputAsync

### 10. Font Availability Documentation
**Issue:** Cascadia Code/JetBrains Mono may not be installed
**Fix:** Added to Phase 3 Limitations section

## Minor Issues Fixed

### 11. UTF-8 Decoding Handling
**Issue:** No mention of malformed data handling
**Fix:** Added note about replacement character handling

### 12. TerminalPanel Layout Specification
**Issue:** Layout not specified
**Fix:** Added Padding = 16px requirement

## Test Improvements

### 13. Mock Framework Specification
**Issue:** Mock framework not specified
**Fix:** Added recommendation to use Mock<ITerminalService>

## Verification Checklist

- [x] Updated all Mono.Posix references to Mono.Posix.NETStandard
- [x] Added forkpty() P/Invoke research to pre-implementation verification
- [x] Added CancellationToken to ITerminalService.StartAsync
- [x] Changed SendInput to SendInputAsync with byte[] parameter
- [x] Increased buffer capacity to 200,000 characters
- [x] Added StatusText integration subscription
- [x] Added auto-focus requirement for terminal TextBox
- [x] Specified O(1) bulk remove trim strategy
- [x] Added font availability limitation
- [x] Added UTF-8 decoding handling note
- [x] Added Padding = 16px layout requirement
- [x] Specified Mock<ITerminalService> for unit tests

## Files Modified

- `/home/cenoda/zaide/docs/phases/phase-3/IMPLEMENTATION_PLAN.md`

## Next Steps

1. Verify Mono.Posix.NETStandard v1.0.0 compatibility with .NET 10
2. Research forkpty() availability in Mono.Posix.NETStandard
3. Implement manual P/Invoke if needed
4. Proceed with Step 1: NuGet + Proof-of-Concept