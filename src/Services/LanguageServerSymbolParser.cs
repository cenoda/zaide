using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Zaide.Features.Language.Application;

namespace Zaide.Services;

/// <summary>Parses document/workspace symbol JSON into structured symbols.</summary>
internal static class LanguageServerSymbolParser
{
    public static LanguageServerSymbolResult? Parse(JsonElement? element)
    {
        if (element is null || element.Value.ValueKind == JsonValueKind.Null)
            return new LanguageServerSymbolResult(Array.Empty<LanguageSymbol>());

        var root = element.Value;
        if (root.ValueKind != JsonValueKind.Array)
            return null;

        var symbols = new List<LanguageSymbol>();
        foreach (var item in root.EnumerateArray())
        {
            if (TryParseSymbol(item, depth: 0, out var symbol))
                symbols.Add(symbol);
        }

        return new LanguageServerSymbolResult(OrderSiblings(symbols));
    }

    private static bool TryParseSymbol(JsonElement element, int depth, out LanguageSymbol symbol)
    {
        symbol = null!;
        if (element.ValueKind != JsonValueKind.Object)
            return false;

        if (!element.TryGetProperty("name", out var nameElement) ||
            nameElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var name = nameElement.GetString();
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var kind = 0;
        if (element.TryGetProperty("kind", out var kindElement) &&
            kindElement.ValueKind == JsonValueKind.Number)
        {
            kindElement.TryGetInt32(out kind);
        }

        string? detail = null;
        if (element.TryGetProperty("detail", out var detailElement) &&
            detailElement.ValueKind == JsonValueKind.String)
        {
            detail = detailElement.GetString();
        }

        string? containerName = null;
        if (element.TryGetProperty("containerName", out var containerElement) &&
            containerElement.ValueKind == JsonValueKind.String)
        {
            containerName = containerElement.GetString();
        }

        LanguageLocation? location = null;

        // DocumentSymbol: selectionRange preferred, else range + (implicit uri from request)
        // Workspace SymbolInformation / WorkspaceSymbol: location object
        if (element.TryGetProperty("location", out var locationElement) &&
            locationElement.ValueKind == JsonValueKind.Object)
        {
            if (TryParseSymbolLocation(locationElement, containerName, name, out var parsedLocation))
                location = parsedLocation;
        }
        else if (element.TryGetProperty("selectionRange", out var selectionRange) &&
                 LanguageServerDefinitionParser.TryParseRange(selectionRange, out var selRange))
        {
            // Hierarchical DocumentSymbol without embedded URI — caller may attach URI.
            location = new LanguageLocation(
                DocumentUri: string.Empty,
                FilePath: null,
                selRange,
                containerName,
                name);
        }
        else if (element.TryGetProperty("range", out var rangeElement) &&
                 LanguageServerDefinitionParser.TryParseRange(rangeElement, out var range))
        {
            location = new LanguageLocation(
                DocumentUri: string.Empty,
                FilePath: null,
                range,
                containerName,
                name);
        }

        var children = new List<LanguageSymbol>();
        if (element.TryGetProperty("children", out var childrenElement) &&
            childrenElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in childrenElement.EnumerateArray())
            {
                if (TryParseSymbol(child, depth + 1, out var childSymbol))
                    children.Add(childSymbol);
            }
        }

        symbol = new LanguageSymbol(
            name,
            kind,
            detail,
            containerName,
            location,
            OrderSiblings(children),
            depth);
        return true;
    }

    private static bool TryParseSymbolLocation(
        JsonElement locationElement,
        string? containerName,
        string? name,
        out LanguageLocation location)
    {
        location = null!;

        if (!locationElement.TryGetProperty("uri", out var uriElement) ||
            uriElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var uri = uriElement.GetString();
        if (string.IsNullOrWhiteSpace(uri))
            return false;

        if (!locationElement.TryGetProperty("range", out var rangeElement) ||
            !LanguageServerDefinitionParser.TryParseRange(rangeElement, out var range))
        {
            return false;
        }

        location = LanguageServerDefinitionParser.CreateLocation(uri, range, containerName, name);
        return true;
    }

    /// <summary>
    /// Attaches a document URI/path to hierarchical DocumentSymbol locations that
    /// lack an embedded URI, then re-orders for deterministic presentation.
    /// </summary>
    public static IReadOnlyList<LanguageSymbol> BindDocumentUri(
        IReadOnlyList<LanguageSymbol> symbols,
        string documentUri,
        string? filePath)
    {
        var normalized = LanguageDocumentUri.Normalize(documentUri);
        return OrderSiblings(symbols.Select(s => BindOne(s, normalized, filePath)).ToList());
    }

    private static LanguageSymbol BindOne(LanguageSymbol symbol, string documentUri, string? filePath)
    {
        LanguageLocation? location = symbol.Location;
        if (location is not null && string.IsNullOrEmpty(location.DocumentUri))
        {
            location = location with
            {
                DocumentUri = documentUri,
                FilePath = filePath,
            };
        }

        var children = symbol.Children
            .Select(c => BindOne(c, documentUri, filePath))
            .ToList();

        return symbol with
        {
            Location = location,
            Children = OrderSiblings(children),
        };
    }

    /// <summary>
    /// Deterministic sibling order: name (OrdinalIgnoreCase), then range start, then URI.
    /// </summary>
    public static IReadOnlyList<LanguageSymbol> OrderSiblings(IReadOnlyList<LanguageSymbol> symbols)
    {
        return symbols
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.Location?.Range.StartLine ?? int.MaxValue)
            .ThenBy(s => s.Location?.Range.StartCharacter ?? int.MaxValue)
            .ThenBy(s => s.Location?.DocumentUri ?? string.Empty, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Flattens hierarchy depth-first for list presentation.</summary>
    public static IReadOnlyList<LanguageSymbol> Flatten(IReadOnlyList<LanguageSymbol> symbols)
    {
        var result = new List<LanguageSymbol>();
        foreach (var symbol in symbols)
            AppendFlattened(symbol, result);
        return result;
    }

    private static void AppendFlattened(LanguageSymbol symbol, List<LanguageSymbol> result)
    {
        result.Add(symbol with { Children = Array.Empty<LanguageSymbol>() });
        foreach (var child in symbol.Children)
            AppendFlattened(child, result);
    }
}
