using ReactiveUI;

namespace Zaide.Features.Debugging.Presentation;

/// <summary>
/// One first-level variable projected from a stopped-state DAP response.
/// </summary>
public sealed class DebugVariableViewModel : ReactiveObject
{
    public DebugVariableViewModel(string name, string value, string? type)
    {
        Name = name;
        Value = value;
        Type = type;
    }

    public string Name { get; }

    public string Value { get; }

    public string? Type { get; }

    public string DisplayText => string.IsNullOrWhiteSpace(Type)
        ? $"{Name} = {Value}"
        : $"{Name} ({Type}) = {Value}";
}