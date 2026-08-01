using System.Globalization;
using System.Text.RegularExpressions;
using LabelPrint.Domain.Enums;
using LabelPrint.Domain.Templates;

namespace LabelPrint.Application.Templates;

/// <summary>
/// Resolves element display text for WYSIWYG preview (mirrors print renderer logic).
/// </summary>
public static partial class TemplatePreviewTextResolver
{
    private static readonly Regex PlaceholderRegex = PlaceholderPattern();

    public static string Resolve(
        TemplateElementType type,
        TextBindingMode bindingMode,
        string? content,
        string? valueBinding,
        IReadOnlyDictionary<string, string> variables)
    {
        if (type is TemplateElementType.Barcode or TemplateElementType.QrCode)
        {
            var code = bindingMode == TextBindingMode.Variable && !string.IsNullOrWhiteSpace(valueBinding)
                ? LookupVariable(variables, valueBinding)
                : ReplacePlaceholders(content ?? "{{Barcode}}", variables);
            return string.IsNullOrWhiteSpace(code) ? "0000000000000" : code;
        }

        return bindingMode switch
        {
            TextBindingMode.Variable when !string.IsNullOrWhiteSpace(valueBinding) =>
                LookupVariable(variables, valueBinding),
            TextBindingMode.CurrentDate =>
                LookupVariable(variables, "Date").Length > 0
                    ? LookupVariable(variables, "Date")
                    : DateTime.Now.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
            TextBindingMode.CurrentTime =>
                LookupVariable(variables, "Time").Length > 0
                    ? LookupVariable(variables, "Time")
                    : DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture),
            _ => ReplacePlaceholders(content ?? string.Empty, variables)
        };
    }

    public static string Resolve(CanvasElementSnapshot element, IReadOnlyDictionary<string, string> variables) =>
        Resolve(element.Type, element.BindingMode, element.Content, element.ValueBinding, variables);

    public sealed record CanvasElementSnapshot(
        TemplateElementType Type,
        TextBindingMode BindingMode,
        string? Content,
        string? ValueBinding);

    private static string ReplacePlaceholders(string text, IReadOnlyDictionary<string, string> variables) =>
        PlaceholderRegex.Replace(text, match => LookupVariable(variables, match.Groups[1].Value));

    private static string LookupVariable(IReadOnlyDictionary<string, string> variables, string key) =>
        variables.TryGetValue(key, out var value) ? value : string.Empty;

    [GeneratedRegex(@"\{\{([^}]+)\}\}", RegexOptions.Compiled)]
    private static partial Regex PlaceholderPattern();
}
