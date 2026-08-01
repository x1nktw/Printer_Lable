namespace LabelPrint.Plugins.Abstractions.Orders;

/// <summary>
/// Optional status surface for order providers (dev banner messaging).
/// </summary>
public interface IOrderProviderStatus
{
    /// <summary>Human-readable status for UI banners.</summary>
    string GetStatusMessage();
}
