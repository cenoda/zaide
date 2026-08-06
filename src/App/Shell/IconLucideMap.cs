using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using Lucide.Avalonia;

namespace Zaide.App.Shell;

/// <summary>
/// Maps stable <c>Icon.*</c> resource keys to Lucide icon kinds.
/// Features use <see cref="IconFactory"/> only — not Lucide types directly.
/// </summary>
internal static class IconLucideMap
{
    private static readonly FrozenDictionary<string, LucideIconKind> Map =
        new Dictionary<string, LucideIconKind>(StringComparer.Ordinal)
        {
            ["Icon.ArrowClockwise"] = LucideIconKind.RotateCw,
            ["Icon.GitBranch"] = LucideIconKind.GitBranch,
            ["Icon.Folder"] = LucideIconKind.Folder,
            ["Icon.Code"] = LucideIconKind.FileCode,
            ["Icon.Text"] = LucideIconKind.FileText,
            ["Icon.Image"] = LucideIconKind.Image,
            ["Icon.Config"] = LucideIconKind.Settings,
            ["Icon.Markup"] = LucideIconKind.CodeXml,
            ["Icon.Project"] = LucideIconKind.Box,
            ["Icon.Unknown"] = LucideIconKind.File,
            ["Icon.X"] = LucideIconKind.X,
            ["Icon.Plus"] = LucideIconKind.Plus,
            ["Icon.Search"] = LucideIconKind.Search,
            ["Icon.Terminal"] = LucideIconKind.Terminal,
            ["Icon.Broom"] = LucideIconKind.Eraser,
            ["Icon.ChevronDown"] = LucideIconKind.ChevronDown,
            ["Icon.ChevronLeft"] = LucideIconKind.ChevronLeft,
            ["Icon.ArrowUp"] = LucideIconKind.ArrowUp,
            ["Icon.Selection"] = LucideIconKind.TextCursor,
            ["Icon.Bell"] = LucideIconKind.Bell,
            ["Icon.Info"] = LucideIconKind.Info,
            ["Icon.Pin"] = LucideIconKind.Pin,
            ["Icon.Warning"] = LucideIconKind.TriangleAlert,
            ["Icon.CheckCircle"] = LucideIconKind.CircleCheck,
            ["Icon.Explorer"] = LucideIconKind.FolderTree,
            ["Icon.SourceControl"] = LucideIconKind.GitBranch,
            ["Icon.Avatar"] = LucideIconKind.CircleUser,
        }.ToFrozenDictionary(StringComparer.Ordinal);

    internal static IReadOnlyCollection<string> AllKeys => Map.Keys;

    internal static LucideIconKind Resolve(string resourceKey)
    {
        if (Map.TryGetValue(resourceKey, out var kind))
            return kind;

        throw new InvalidOperationException($"Icon resource '{resourceKey}' was not found.");
    }
}
