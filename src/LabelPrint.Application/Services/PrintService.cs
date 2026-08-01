using System.Text.Json;
using LabelPrint.Application.Abstractions.Repositories;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Application.Common;
using LabelPrint.Application.Options;
using LabelPrint.Application.Templates;
using LabelPrint.Domain.Entities;
using LabelPrint.Domain.Enums;
using LabelPrint.Domain.Exceptions;
using LabelPrint.Plugins.Abstractions.Printing;
using LabelPrint.Plugins.Abstractions.Variables;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace LabelPrint.Application.Services;
/// <summary>
/// Orchestrates label rendering and printer gateway dispatch.
/// </summary>
public sealed class PrintService : IPrintService, IPrintJobProcessor
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILabelRenderService _renderService;
    private readonly IVariableResolver _variableResolver;
    private readonly IPrinterGateway _printerGateway;
    private readonly ILabelDateTimeService _labelDateTime;
    private readonly PrintQueueOptions _queueOptions;
    private readonly ILogger<PrintService> _logger;

    public PrintService(
        IUnitOfWork unitOfWork,
        ILabelRenderService renderService,
        IVariableResolver variableResolver,
        IPrinterGateway printerGateway,
        ILabelDateTimeService labelDateTime,
        IOptions<PrintQueueOptions> queueOptions,
        ILogger<PrintService> logger)
    {
        _unitOfWork = unitOfWork;
        _renderService = renderService;
        _variableResolver = variableResolver;
        _printerGateway = printerGateway;
        _labelDateTime = labelDateTime;
        _queueOptions = queueOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> PrintProductAsync(
        Guid productId,
        Guid? printerId = null,
        int copies = 1,
        DateTimeOffset? labelDateTimeOverride = null,
        CancellationToken cancellationToken = default)
    {
        if (copies < 1)
        {
            return Result.Failure<Guid>("Copies must be at least 1.");
        }

        var product = await _unitOfWork.Products.GetByIdAsync(productId, cancellationToken);
        if (product is null || product.IsArchived)
        {
            return Result.Failure<Guid>("Product not found.");
        }

        var templateId = product.DefaultTemplateId;
        LabelTemplate? template = null;
        if (templateId is Guid tid)
        {
            template = await _unitOfWork.Templates.GetByIdAsync(tid, cancellationToken);
        }

        if (template is null || template.IsArchived)
        {
            var search = await _unitOfWork.Templates.SearchAsync(null, includeArchived: false, skip: 0, take: 1, cancellationToken);
            template = search.Items.FirstOrDefault();
            templateId = template?.Id;
        }

        if (template is null || templateId is null)
        {
            return Result.Failure<Guid>("No label template available. Create a template first.");
        }

        var printer = printerId is Guid pid
            ? await _unitOfWork.Printers.GetByIdAsync(pid, cancellationToken)
            : await _unitOfWork.Printers.GetDefaultAsync(cancellationToken)
              ?? (await _unitOfWork.Printers.GetAllAsync(includeInactive: false, cancellationToken)).FirstOrDefault();
        if (printer is null || !printer.IsActive)
        {
            return Result.Failure<Guid>("No active printer configured. Add a printer in the Printers section.");
        }

        var stamp = await BuildDateTimeValuesAsync(labelDateTimeOverride, cancellationToken);
        var variableContext = new VariableContext
        {
            ProductId = productId,
            Values = new Dictionary<string, string>(stamp, StringComparer.OrdinalIgnoreCase)
            {
                ["PriceAmount"] = product.PriceAmount.ToString("0.##"),
                ["Currency"] = product.PriceCurrency,
                ["ProductName"] = product.Name
            }
        };

        var variables = await _variableResolver.ResolveAllAsync(variableContext, cancellationToken);
        var variablesJson = JsonSerializer.Serialize(variables);
        var job = new PrintJob
        {
            PrinterId = printer.Id,
            TemplateId = templateId.Value,
            ProductId = productId,
            Copies = copies,
            Title = $"{product.Name} ({product.Sku})",
            VariablesJson = variablesJson
        };
        await _unitOfWork.PrintJobs.AddAsync(job, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var processSync = _queueOptions.ProcessSynchronously || !_queueOptions.UseBackgroundWorker;
        if (!processSync)
        {
            _logger.LogInformation("Print job {JobId} enqueued for printer {PrinterName}", job.Id, printer.Name);
            return Result.Success(job.Id);
        }

        try
        {
            await ProcessJobAsync(job, template, printer, variables, cancellationToken);
            return Result.Success(job.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Print job {JobId} failed", job.Id);
            await HandleJobFailureAsync(job, template, product, printer, variablesJson, ex, maxRetries: 0, cancellationToken);
            return Result.Failure<Guid>(ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> PrintRawLabelAsync(
        string name,
        Guid? printerId = null,
        int copies = 1,
        DateTimeOffset? labelDateTimeOverride = null,
        Guid? productId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name) && productId is null)
        {
            return Result.Failure<Guid>("Укажите название сырья.");
        }

        if (copies < 1)
        {
            return Result.Failure<Guid>("Copies must be at least 1.");
        }

        Product? product = null;
        if (productId is Guid pidProduct)
        {
            product = await _unitOfWork.Products.GetByIdAsync(pidProduct, cancellationToken);
            if (product is null || product.IsArchived)
            {
                return Result.Failure<Guid>("Product not found.");
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                name = product.Name;
            }
        }

        LabelTemplate? template = null;
        if (product?.DefaultTemplateId is Guid tid)
        {
            template = await _unitOfWork.Templates.GetByIdAsync(tid, cancellationToken);
        }

        if (template is null || template.IsArchived)
        {
            var byName = await _unitOfWork.Templates.SearchAsync("Сырьё", includeArchived: false, skip: 0, take: 5, cancellationToken);
            template = byName.Items.FirstOrDefault(t => t.Name.Contains("Сырьё", StringComparison.OrdinalIgnoreCase))
                       ?? byName.Items.FirstOrDefault();
        }

        if (template is null || template.IsArchived)
        {
            var search = await _unitOfWork.Templates.SearchAsync(null, includeArchived: false, skip: 0, take: 1, cancellationToken);
            template = search.Items.FirstOrDefault();
        }

        if (template is null)
        {
            return Result.Failure<Guid>("Нет шаблона для печати. Создайте шаблон «Сырьё».");
        }

        var printer = printerId is Guid pid
            ? await _unitOfWork.Printers.GetByIdAsync(pid, cancellationToken)
            : await _unitOfWork.Printers.GetDefaultAsync(cancellationToken)
              ?? (await _unitOfWork.Printers.GetAllAsync(includeInactive: false, cancellationToken)).FirstOrDefault();
        if (printer is null || !printer.IsActive)
        {
            return Result.Failure<Guid>("No active printer configured. Add a printer in the Printers section.");
        }

        var stamp = await BuildDateTimeValuesAsync(labelDateTimeOverride, cancellationToken);
        var values = new Dictionary<string, string>(stamp, StringComparer.OrdinalIgnoreCase)
        {
            ["ProductName"] = name.Trim()
        };
        if (product is not null)
        {
            values["PriceAmount"] = product.PriceAmount.ToString("0.##");
            values["Currency"] = product.PriceCurrency;
            if (!string.IsNullOrWhiteSpace(product.Sku))
            {
                values["Sku"] = product.Sku;
            }
        }

        var variableContext = new VariableContext
        {
            ProductId = product?.Id,
            Values = values
        };
        var variables = await _variableResolver.ResolveAllAsync(variableContext, cancellationToken);
        var variablesJson = JsonSerializer.Serialize(variables);
        var job = new PrintJob
        {
            PrinterId = printer.Id,
            TemplateId = template.Id,
            ProductId = product?.Id,
            Copies = copies,
            Title = $"Сырьё: {name.Trim()}",
            VariablesJson = variablesJson
        };
        await _unitOfWork.PrintJobs.AddAsync(job, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var processSync = _queueOptions.ProcessSynchronously || !_queueOptions.UseBackgroundWorker;
        if (!processSync)
        {
            return Result.Success(job.Id);
        }

        try
        {
            await ProcessJobAsync(job, template, printer, variables, cancellationToken);
            return Result.Success(job.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Raw print job {JobId} failed", job.Id);
            await HandleJobFailureAsync(job, template, product, printer, variablesJson, ex, maxRetries: 0, cancellationToken);
            return Result.Failure<Guid>(ex.Message);
        }
    }

    private async Task<Dictionary<string, string>> BuildDateTimeValuesAsync(
        DateTimeOffset? labelDateTimeOverride,
        CancellationToken cancellationToken)
    {
        var effective = await _labelDateTime.GetEffectiveAsync(labelDateTimeOverride, cancellationToken);
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Date"] = _labelDateTime.FormatDate(effective),
            ["Time"] = _labelDateTime.FormatTime(effective)
        };
    }
    /// <inheritdoc />
    public async Task<Result<Guid>> PrintOrderItemAsync(
        Guid orderItemId,
        Guid? printerId = null,
        int copies = 1,
        CancellationToken cancellationToken = default)
    {
        if (copies < 1)
        {
            return Result.Failure<Guid>("Copies must be at least 1.");
        }
        var orderItem = await _unitOfWork.Orders.GetItemByIdAsync(orderItemId, cancellationToken);
        if (orderItem is null)
        {
            return Result.Failure<Guid>("Order item not found.");
        }
        var order = orderItem.Order;
        if (order is null)
        {
            return Result.Failure<Guid>("Order not found.");
        }
        Product? product = null;
        Guid? templateId = null;
        if (orderItem.ProductId is Guid productId)
        {
            product = await _unitOfWork.Products.GetByIdAsync(productId, cancellationToken);
            templateId = product?.ResolveOrderItemTemplateId();
        }
        LabelTemplate? template = null;
        if (templateId is Guid tid)
        {
            template = await _unitOfWork.Templates.GetByIdAsync(tid, cancellationToken);
        }
        if (template is null || template.IsArchived)
        {
            template = await FindKitchenTemplateAsync(cancellationToken);
            templateId = template?.Id;
        }
        if (template is null || template.IsArchived)
        {
            var search = await _unitOfWork.Templates.SearchAsync(null, includeArchived: false, skip: 0, take: 1, cancellationToken);
            template = search.Items.FirstOrDefault();
            templateId = template?.Id;
        }
        if (template is null || templateId is null)
        {
            return Result.Failure<Guid>("No label template available. Create a template first.");
        }
        var printer = printerId is Guid pid
            ? await _unitOfWork.Printers.GetByIdAsync(pid, cancellationToken)
            : await _unitOfWork.Printers.GetDefaultAsync(cancellationToken)
              ?? (await _unitOfWork.Printers.GetAllAsync(includeInactive: false, cancellationToken)).FirstOrDefault();
        if (printer is null || !printer.IsActive)
        {
            return Result.Failure<Guid>("No active printer configured. Add a printer in the Printers section.");
        }
        var stamp = await BuildDateTimeValuesAsync(null, cancellationToken);
        var (positionName, addonsSection, addonsList) = SplitPositionNameAndAddons(orderItem);
        var values = new Dictionary<string, string>(stamp, StringComparer.OrdinalIgnoreCase)
        {
            ["OrderNumber"] = order.Number,
            ["PositionName"] = positionName,
            ["PositionIndex"] = orderItem.PositionIndex.ToString(),
            ["PositionTotal"] = orderItem.PositionTotal.ToString(),
            ["ProductName"] = product?.Name ?? positionName,
            ["PriceAmount"] = orderItem.Price?.ToString("0.##") ?? string.Empty,
            ["Currency"] = product?.PriceCurrency ?? "RUB",
            ["Addons"] = addonsList,
            ["AddonsKitchen"] = addonsList,
            ["AddonsSection"] = addonsSection
        };
        var variableContext = new VariableContext
        {
            ProductId = product?.Id,
            OrderId = order.Id,
            OrderItemId = orderItem.Id,
            Values = values
        };
        var variables = await _variableResolver.ResolveAllAsync(variableContext, cancellationToken);
        var variablesJson = JsonSerializer.Serialize(variables);
        var job = new PrintJob
        {
            PrinterId = printer.Id,
            TemplateId = templateId,
            ProductId = product?.Id,
            OrderId = order.Id,
            OrderItemId = orderItem.Id,
            ExternalOrderId = order.ExternalOrderId,
            Copies = copies,
            Title = $"{order.Number}: {orderItem.Name} ({orderItem.PositionIndex}/{orderItem.PositionTotal})",
            VariablesJson = variablesJson
        };
        await _unitOfWork.PrintJobs.AddAsync(job, cancellationToken);
        orderItem.IsPrinted = true;
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var processSync = _queueOptions.ProcessSynchronously || !_queueOptions.UseBackgroundWorker;
        if (!processSync)
        {
            _logger.LogInformation("Order item print job {JobId} enqueued", job.Id);
            return Result.Success(job.Id);
        }
        try
        {
            await ProcessJobAsync(job, template, printer, variables, cancellationToken);
            return Result.Success(job.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Order item print job {JobId} failed", job.Id);
            await HandleJobFailureAsync(job, template, product, printer, variablesJson, ex, maxRetries: 0, cancellationToken);
            return Result.Failure<Guid>(ex.Message);
        }
    }
    /// <inheritdoc />
    public async Task ProcessClaimedJobAsync(PrintJob job, int maxRetries, CancellationToken cancellationToken = default)
    {
        LabelTemplate? template = null;
        Printer? printer = null;
        Product? product = null;
        try
        {
            template = job.TemplateId is Guid templateId
                ? await _unitOfWork.Templates.GetByIdAsync(templateId, cancellationToken)
                : null;
            if (template is null || template.IsArchived)
            {
                throw new InvalidOperationException("Template not found or archived.");
            }
            printer = await _unitOfWork.Printers.GetByIdAsync(job.PrinterId, cancellationToken);
            if (printer is null || !printer.IsActive)
            {
                throw new InvalidOperationException("Printer not found or inactive.");
            }
            product = job.ProductId is Guid productId
                ? await _unitOfWork.Products.GetByIdAsync(productId, cancellationToken)
                : null;
            var variables = DeserializeVariables(job.VariablesJson);
            await ProcessJobAsync(job, template, printer, variables, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Print job {JobId} failed during processing", job.Id);
            await HandleJobFailureAsync(job, template, product, printer, job.VariablesJson, ex, maxRetries, cancellationToken);
        }
    }
    private async Task ProcessJobAsync(
        PrintJob job,
        LabelTemplate template,
        Printer printer,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken cancellationToken)
    {
        job.MarkAsRendering();
        _unitOfWork.PrintJobs.Update(job);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        var document = TemplateDocumentSerializer.Deserialize(template.ContentJson);
        var rendered = await _renderService.RenderAsync(document, variables, cancellationToken);
        job.MarkAsPrinting();
        _unitOfWork.PrintJobs.Update(job);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _printerGateway.PrintAsync(printer.Id, rendered, job.Copies, cancellationToken);
        job.MarkAsCompleted();
        _unitOfWork.PrintJobs.Update(job);
        var product = job.ProductId is Guid pid
            ? await _unitOfWork.Products.GetByIdAsync(pid, cancellationToken)
            : null;
        await AddHistoryAsync(job, template, product, printer, job.VariablesJson, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Print job {JobId} completed on printer {PrinterName}", job.Id, printer.Name);
    }
    private async Task HandleJobFailureAsync(
        PrintJob job,
        LabelTemplate? template,
        Product? product,
        Printer? printer,
        string variablesJson,
        Exception ex,
        int maxRetries,
        CancellationToken cancellationToken)
    {
        var isTransient = IsTransientException(ex);
        try
        {
            job.MarkAsFailed(ex.Message, isTransient);
        }
        catch (DomainException)
        {
            // Job may already be in Failed from a partial state — leave as-is.
        }
        _unitOfWork.PrintJobs.Update(job);
        if (isTransient && job.RetryCount < maxRetries)
        {
            try
            {
                job.RequeueForRetry();
                _unitOfWork.PrintJobs.Update(job);
                _logger.LogWarning(
                    "Print job {JobId} requeued for retry ({RetryCount}/{MaxRetries})",
                    job.Id,
                    job.RetryCount,
                    maxRetries);
            }
            catch (DomainException retryEx)
            {
                _logger.LogWarning(retryEx, "Could not requeue job {JobId}", job.Id);
            }
        }
        else if (template is not null && printer is not null)
        {
            await AddHistoryAsync(job, template, product, printer, variablesJson, cancellationToken);
        }
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
    private async Task AddHistoryAsync(
        PrintJob job,
        LabelTemplate template,
        Product? product,
        Printer printer,
        string variablesJson,
        CancellationToken cancellationToken)
    {
        var entry = new PrintHistory
        {
            PrintJobId = job.Id,
            SourceJobId = job.SourceJobId,
            Status = job.Status,
            Description = job.Title,
            PrinterId = printer.Id,
            PrinterName = printer.Name,
            TemplateId = template.Id,
            TemplateName = template.Name,
            ProductId = product?.Id,
            ProductName = product?.Name,
            Copies = job.Copies,
            FailureReason = job.FailureReason,
            VariablesJson = variablesJson,
            PrintedAt = DateTimeOffset.UtcNow
        };
        await _unitOfWork.PrintHistory.AddAsync(entry, cancellationToken);
    }
    private async Task<LabelTemplate?> FindKitchenTemplateAsync(CancellationToken cancellationToken)
    {
        foreach (var key in new[] { "Кухня чек 40", "Кухня чек", "Кухня 58", "Позиция заказа" })
        {
            var search = await _unitOfWork.Templates.SearchAsync(key, includeArchived: false, skip: 0, take: 8, cancellationToken);
            var hit = search.Items.FirstOrDefault(t =>
                t.Name.Contains(key, StringComparison.OrdinalIgnoreCase));
            if (hit is not null)
            {
                return hit;
            }
        }

        return null;
    }

    /// <summary>
    /// Splits dish name and add-ons for kitchen labels (supports legacy "Name + A, B" and comment payloads).
    /// </summary>
    internal static (string PositionName, string AddonsSection, string AddonsList) SplitPositionNameAndAddons(OrderItem item)
    {
        var positionName = item.Name?.Trim() ?? string.Empty;
        var addons = new List<string>();

        if (!string.IsNullOrWhiteSpace(item.Comment))
        {
            var comment = item.Comment.Trim();
            if (comment.StartsWith("добавки:", StringComparison.OrdinalIgnoreCase))
            {
                addons.AddRange(comment[8..]
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }
            else
            {
                addons.AddRange(comment.Split(
                    ['\r', '\n'],
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }
        }

        const string mergeMarker = " + ";
        var mergeIdx = positionName.IndexOf(mergeMarker, StringComparison.Ordinal);
        if (mergeIdx > 0)
        {
            var tail = positionName[(mergeIdx + mergeMarker.Length)..];
            positionName = positionName[..mergeIdx].Trim();
            if (addons.Count == 0)
            {
                addons.AddRange(tail.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }
        }

        addons = addons
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var list = string.Join("\n", addons);
        var section = addons.Count == 0
            ? string.Empty
            : "ДОБАВКИ:\n" + string.Join("\n", addons.Select(a => "• " + a));
        return (positionName, section, list);
    }

    private static IReadOnlyDictionary<string, string> DeserializeVariables(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
               ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
    internal static bool IsTransientException(Exception ex) =>
        ex is IOException or TimeoutException or UnauthorizedAccessException
        || (ex.InnerException is not null && IsTransientException(ex.InnerException));
}
