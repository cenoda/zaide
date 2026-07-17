using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Zaide.Features.Debugging.Infrastructure.Dap;

/// <summary>
/// Parses stopped-state DAP inspection responses into immutable records.
/// </summary>
public static class DapInspectionParser
{
    public static IReadOnlyList<DapThreadInfo> ParseThreads(JsonElement? response)
    {
        if (response is not { ValueKind: JsonValueKind.Object } body ||
            !body.TryGetProperty("threads", out var threadsElement) ||
            threadsElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<DapThreadInfo>();
        }

        var threads = new List<DapThreadInfo>();
        foreach (var threadElement in threadsElement.EnumerateArray())
        {
            if (threadElement.ValueKind != JsonValueKind.Object ||
                !threadElement.TryGetProperty("id", out var idElement) ||
                idElement.ValueKind != JsonValueKind.Number ||
                !idElement.TryGetInt32(out var id))
            {
                continue;
            }

            string name = $"Thread {id}";
            if (threadElement.TryGetProperty("name", out var nameElement) &&
                nameElement.ValueKind == JsonValueKind.String)
            {
                var parsedName = nameElement.GetString();
                if (!string.IsNullOrWhiteSpace(parsedName))
                    name = parsedName;
            }

            threads.Add(new DapThreadInfo(id, name));
        }

        return threads;
    }

    public static IReadOnlyList<DapStackFrameInfo> ParseStackFrames(JsonElement? response)
    {
        if (response is not { ValueKind: JsonValueKind.Object } body ||
            !body.TryGetProperty("stackFrames", out var framesElement) ||
            framesElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<DapStackFrameInfo>();
        }

        var frames = new List<DapStackFrameInfo>();
        foreach (var frameElement in framesElement.EnumerateArray())
        {
            if (frameElement.ValueKind != JsonValueKind.Object ||
                !frameElement.TryGetProperty("id", out var idElement) ||
                idElement.ValueKind != JsonValueKind.Number ||
                !idElement.TryGetInt32(out var id))
            {
                continue;
            }

            string name = $"Frame {id}";
            if (frameElement.TryGetProperty("name", out var nameElement) &&
                nameElement.ValueKind == JsonValueKind.String)
            {
                var parsedName = nameElement.GetString();
                if (!string.IsNullOrWhiteSpace(parsedName))
                    name = parsedName;
            }

            string? sourcePath = null;
            int? line = null;

            if (frameElement.TryGetProperty("source", out var sourceElement) &&
                sourceElement.ValueKind == JsonValueKind.Object &&
                sourceElement.TryGetProperty("path", out var pathElement) &&
                pathElement.ValueKind == JsonValueKind.String)
            {
                sourcePath = NormalizeSourcePath(pathElement.GetString());
            }

            if (frameElement.TryGetProperty("line", out var lineElement) &&
                lineElement.ValueKind == JsonValueKind.Number &&
                lineElement.TryGetInt32(out var parsedLine) &&
                parsedLine >= 1)
            {
                line = parsedLine;
            }

            frames.Add(new DapStackFrameInfo(id, name, sourcePath, line));
        }

        return frames;
    }

    public static IReadOnlyList<DapScopeInfo> ParseScopes(JsonElement? response)
    {
        if (response is not { ValueKind: JsonValueKind.Object } body ||
            !body.TryGetProperty("scopes", out var scopesElement) ||
            scopesElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<DapScopeInfo>();
        }

        var scopes = new List<DapScopeInfo>();
        foreach (var scopeElement in scopesElement.EnumerateArray())
        {
            if (scopeElement.ValueKind != JsonValueKind.Object ||
                !scopeElement.TryGetProperty("variablesReference", out var referenceElement) ||
                referenceElement.ValueKind != JsonValueKind.Number ||
                !referenceElement.TryGetInt32(out var variablesReference) ||
                variablesReference <= 0)
            {
                continue;
            }

            string name = "Scope";
            if (scopeElement.TryGetProperty("name", out var nameElement) &&
                nameElement.ValueKind == JsonValueKind.String)
            {
                var parsedName = nameElement.GetString();
                if (!string.IsNullOrWhiteSpace(parsedName))
                    name = parsedName;
            }

            scopes.Add(new DapScopeInfo(name, variablesReference));
        }

        return scopes;
    }

    public static IReadOnlyList<DapVariableInfo> ParseVariables(JsonElement? response)
    {
        if (response is not { ValueKind: JsonValueKind.Object } body ||
            !body.TryGetProperty("variables", out var variablesElement) ||
            variablesElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<DapVariableInfo>();
        }

        var variables = new List<DapVariableInfo>();
        foreach (var variableElement in variablesElement.EnumerateArray())
        {
            if (variableElement.ValueKind != JsonValueKind.Object ||
                !variableElement.TryGetProperty("name", out var nameElement) ||
                nameElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var name = nameElement.GetString();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            string value = string.Empty;
            if (variableElement.TryGetProperty("value", out var valueElement) &&
                valueElement.ValueKind == JsonValueKind.String)
            {
                value = valueElement.GetString() ?? string.Empty;
            }

            string? type = null;
            if (variableElement.TryGetProperty("type", out var typeElement) &&
                typeElement.ValueKind == JsonValueKind.String)
            {
                type = typeElement.GetString();
            }

            variables.Add(new DapVariableInfo(name, value, type));
        }

        return variables;
    }

    private static string? NormalizeSourcePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            if (!Path.IsPathRooted(path))
                return null;

            return Path.GetFullPath(path);
        }
        catch
        {
            return null;
        }
    }
}