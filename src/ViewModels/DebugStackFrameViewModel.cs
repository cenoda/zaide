using ReactiveUI;

namespace Zaide.ViewModels;

/// <summary>
/// One call-stack frame projected from a stopped-state DAP response.
/// </summary>
public sealed class DebugStackFrameViewModel : ReactiveObject
{
    public DebugStackFrameViewModel(int id, string name, string? sourcePath, int? line)
    {
        Id = id;
        Name = name;
        SourcePath = sourcePath;
        Line = line;
    }

    public int Id { get; }

    public string Name { get; }

    public string? SourcePath { get; }

    public int? Line { get; }

    public string DisplayText
    {
        get
        {
            if (SourcePath is null || Line is null)
                return Name;

            return $"{Name} — {System.IO.Path.GetFileName(SourcePath)}:{Line}";
        }
    }
}