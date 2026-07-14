using ReactiveUI;

namespace Zaide.ViewModels;

/// <summary>
/// One debug thread projected from a stopped-state DAP response.
/// </summary>
public sealed class DebugThreadViewModel : ReactiveObject
{
    public DebugThreadViewModel(int id, string name)
    {
        Id = id;
        Name = name;
    }

    public int Id { get; }

    public string Name { get; }

    public string DisplayText => $"{Name} ({Id})";
}