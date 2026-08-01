using LabelPrint.Domain.Entities;

namespace LabelPrint.Application.Abstractions.Repositories;

/// <summary>
/// Persistence port for <see cref="Printer"/> entities.
/// </summary>
public interface IPrinterRepository
{
    Task<Printer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Printer?> GetDefaultAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Printer>> GetAllAsync(bool includeInactive = false, CancellationToken cancellationToken = default);

    Task AddAsync(Printer printer, CancellationToken cancellationToken = default);

    void Update(Printer printer);

    Task DeactivateAsync(Guid id, CancellationToken cancellationToken = default);

    Task ClearDefaultFlagAsync(CancellationToken cancellationToken = default);
}
