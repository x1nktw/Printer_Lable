using LabelPrint.Domain.Entities;

namespace LabelPrint.Application.Abstractions.Repositories;

/// <summary>
/// Local user persistence port.
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<User>> GetActiveUsersAsync(CancellationToken cancellationToken = default);
}
