using Zaide.Features.Settings.Domain;
using Zaide.Features.Settings.Contracts;
namespace Zaide.Features.Settings.Presentation;

/// <summary>
/// One row in a <see cref="SettingsFontPicker"/> list.
/// </summary>
/// <param name="Name">Primary font family name stored in settings.</param>
/// <param name="IsAvailable">Whether the family is installed and previewable.</param>
/// <param name="DisplayText">Label shown in the picker row.</param>
public sealed record FontPickerEntry(string Name, bool IsAvailable, string DisplayText)
{
    /// <summary>Creates a row for an installed system font.</summary>
    public static FontPickerEntry Available(string name) => new(name, true, name);

    /// <summary>Creates a row for a persisted font that is not installed.</summary>
    public static FontPickerEntry Unavailable(string name) => new(name, false, $"{name} (unavailable)");
}
