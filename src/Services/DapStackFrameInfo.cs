namespace Zaide.Services;

/// <summary>
/// Parsed DAP stack frame entry.
/// </summary>
/// <param name="Id">Adapter frame id.</param>
/// <param name="Name">Frame display name.</param>
/// <param name="SourcePath">Normalized absolute on-disk source path when local.</param>
/// <param name="Line">One-based source line when supplied.</param>
public sealed record DapStackFrameInfo(int Id, string Name, string? SourcePath, int? Line);