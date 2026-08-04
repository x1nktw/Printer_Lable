using System.Text.Json;
using System.Text.Json.Serialization;
using LabelPrint.Domain.Templates;

namespace LabelPrint.Application.Templates;

/// <summary>
/// Serializes / deserializes versioned template documents.
/// </summary>
public static class TemplateDocumentSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>Deserializes template JSON; returns empty document on invalid input.</summary>
    public static TemplateDocument Deserialize(string? json)
    {
        if (!TryDeserialize(json, out var document))
        {
            return new TemplateDocument { SchemaVersion = 1 };
        }

        return document;
    }

    /// <summary>Tries to parse a template export; fails on empty/invalid JSON.</summary>
    public static bool TryDeserialize(string? json, out TemplateDocument document)
    {
        document = new TemplateDocument();
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<TemplateDocument>(json, Options);
            if (parsed is null)
            {
                return false;
            }

            if (parsed.Canvas.WidthMm <= 0 || parsed.Canvas.HeightMm <= 0)
            {
                return false;
            }

            document = parsed;
            document.SchemaVersion = document.SchemaVersion <= 0 ? 1 : document.SchemaVersion;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>Serializes a template document to JSON.</summary>
    public static string Serialize(TemplateDocument document)
    {
        document.SchemaVersion = document.SchemaVersion <= 0 ? 1 : document.SchemaVersion;
        return JsonSerializer.Serialize(document, Options);
    }
}
