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
        if (string.IsNullOrWhiteSpace(json))
        {
            return new TemplateDocument();
        }

        try
        {
            return JsonSerializer.Deserialize<TemplateDocument>(json, Options) ?? new TemplateDocument();
        }
        catch (JsonException)
        {
            return new TemplateDocument { SchemaVersion = 1 };
        }
    }

    /// <summary>Serializes a template document to JSON.</summary>
    public static string Serialize(TemplateDocument document)
    {
        document.SchemaVersion = document.SchemaVersion <= 0 ? 1 : document.SchemaVersion;
        return JsonSerializer.Serialize(document, Options);
    }
}
