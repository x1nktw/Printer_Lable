using LabelPrint.Domain.Entities;

namespace LabelPrint.Application.Abstractions.Repositories;

/// <summary>
/// Unit of Work coordinating repository commits.
/// </summary>
public interface IUnitOfWork
{
    IProductRepository Products { get; }

    ICategoryRepository Categories { get; }

    ICustomFieldDefinitionRepository CustomFieldDefinitions { get; }

    ITemplateRepository Templates { get; }

    IPrintJobRepository PrintJobs { get; }

    IPrintHistoryRepository PrintHistory { get; }

    IPrinterRepository Printers { get; }

    IAppSettingsRepository Settings { get; }

    IUserRepository Users { get; }

    IOrderRepository Orders { get; }

    IAddonRepository Addons { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
