using ReactiveUI;

namespace Zaide.ViewModels;

/// <summary>
/// One scope projected from a stopped-state DAP response.
/// </summary>
public sealed class DebugScopeViewModel : ReactiveObject
{
    public DebugScopeViewModel(string name, int variablesReference)
    {
        Name = name;
        VariablesReference = variablesReference;
    }

    public string Name { get; }

    public int VariablesReference { get; }
}