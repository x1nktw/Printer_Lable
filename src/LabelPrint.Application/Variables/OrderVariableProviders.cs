using LabelPrint.Application.Abstractions.Repositories;
using LabelPrint.Plugins.Abstractions.Variables;

namespace LabelPrint.Application.Variables;

/// <summary>Resolves order number from explicit context values.</summary>
public sealed class OrderNumberVariableProvider : ContextValueVariableProvider
{
    public override string Key => "OrderNumber";
    public override string DisplayName => "Номер заказа";
}

/// <summary>Resolves position name from explicit context values.</summary>
public sealed class PositionNameVariableProvider : ContextValueVariableProvider
{
    public override string Key => "PositionName";
    public override string DisplayName => "Название позиции";
}

/// <summary>Resolves position index (N) from explicit context values.</summary>
public sealed class PositionIndexVariableProvider : ContextValueVariableProvider
{
    public override string Key => "PositionIndex";
    public override string DisplayName => "Позиция N";
}

/// <summary>Resolves position total (M) from explicit context values.</summary>
public sealed class PositionTotalVariableProvider : ContextValueVariableProvider
{
    public override string Key => "PositionTotal";
    public override string DisplayName => "Позиций всего";
}

/// <summary>Base for variables supplied via <see cref="VariableContext.Values"/>.</summary>
public abstract class ContextValueVariableProvider : IVariableProvider
{
    public abstract string Key { get; }

    public abstract string DisplayName { get; }

    public Task<string> ResolveAsync(VariableContext context, CancellationToken cancellationToken = default)
    {
        if (context.Values.TryGetValue(Key, out var value))
        {
            return Task.FromResult(value);
        }

        return Task.FromResult(string.Empty);
    }
}
