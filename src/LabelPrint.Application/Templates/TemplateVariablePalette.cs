namespace LabelPrint.Application.Templates;

/// <summary>
/// Known template variable bindings for the editor palette.
/// </summary>
public static class TemplateVariablePalette
{
    public sealed record VariableDefinition(string Key, string DisplayName, string SampleValue);

    public static IReadOnlyList<VariableDefinition> KnownVariables { get; } =
    [
        new("ProductName", "Название товара", "Образец товара"),
        new("Sku", "Артикул (SKU)", "SKU-001"),
        new("Barcode", "Штрихкод", "4601234567890"),
        new("Price", "Цена", "99.90"),
        new("Date", "Дата", DateTime.Now.ToString("dd.MM.yyyy")),
        new("Time", "Время", DateTime.Now.ToString("HH:mm")),
        new("ExpireDate", "Срок годности", DateTime.Now.AddMonths(6).ToString("dd.MM.yyyy")),
        new("OrderNumber", "№ заказа", "65502"),
        new("PositionName", "Позиция заказа", "Шаверма Сырная"),
        new("AddonsKitchen", "Добавки (кухня)", "Добавить халапеньо\nДвойной сыр\nБез лука"),
        new("AddonsSection", "Блок добавок", "ДОБАВКИ:\n• Двойной сыр"),
        new("Addons", "Список добавок", "Двойной сыр"),
        new("PositionIndex", "Индекс позиции", "2"),
        new("PositionTotal", "Всего позиций", "3"),
        new("Custom.Field", "Custom.* (поле)", "Значение")
    ];
}
