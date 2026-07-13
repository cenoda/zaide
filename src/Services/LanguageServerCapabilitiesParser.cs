using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Zaide.Services;

/// <summary>
/// Parses <see cref="LanguageServerCapabilities"/> from an LSP initialize result.
/// </summary>
internal static class LanguageServerCapabilitiesParser
{
    public static LanguageServerCapabilities Parse(JsonElement? initializeResult)
    {
        if (initializeResult is not { ValueKind: JsonValueKind.Object } root ||
            !root.TryGetProperty("capabilities", out var capabilities) ||
            capabilities.ValueKind != JsonValueKind.Object)
        {
            return LanguageServerCapabilities.None;
        }

        var completionSupported = false;
        var triggerCharacters = Array.Empty<char>();

        if (capabilities.TryGetProperty("completionProvider", out var completionProvider) &&
            completionProvider.ValueKind == JsonValueKind.Object)
        {
            completionSupported = true;
            if (completionProvider.TryGetProperty("triggerCharacters", out var triggers) &&
                triggers.ValueKind == JsonValueKind.Array)
            {
                var chars = new List<char>();
                foreach (var item in triggers.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String)
                        continue;

                    var text = item.GetString();
                    if (!string.IsNullOrEmpty(text))
                        chars.Add(text[0]);
                }

                triggerCharacters = chars.ToArray();
            }
        }

        var hoverSupported = capabilities.TryGetProperty("hoverProvider", out var hoverProvider) &&
                             hoverProvider.ValueKind is JsonValueKind.True or JsonValueKind.Object;

        return new LanguageServerCapabilities(completionSupported, triggerCharacters, hoverSupported);
    }
}
