using LabelPrint.Domain.Common;
using LabelPrint.Domain.Enums;

namespace LabelPrint.Domain.Entities;

/// <summary>
/// Immutable audit snapshot materialized when a print job finishes.
/// </summary>
public class PrintHistory : EntityBase
{
    public DateTimeOffset PrintedAt { get; set; } = DateTimeOffset.UtcNow;

    public Guid? PrintJobId { get; set; }

    public Guid? SourceJobId { get; set; }

    public PrintJobStatus Status { get; set; }

    public string Description { get; set; } = string.Empty;

    public Guid? UserId { get; set; }

    public string? UserName { get; set; }

    public Guid? PrinterId { get; set; }

    public string? PrinterName { get; set; }

    public Guid? TemplateId { get; set; }

    public string? TemplateName { get; set; }

    public Guid? ProductId { get; set; }

    public string? ProductName { get; set; }

    public Guid? OrderId { get; set; }

    public string? OrderNumber { get; set; }

    public Guid? OrderItemId { get; set; }

    public int Copies { get; set; } = 1;

    public string? FailureReason { get; set; }

    public string VariablesJson { get; set; } = "{}";
}
