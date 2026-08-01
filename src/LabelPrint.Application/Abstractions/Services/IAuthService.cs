using LabelPrint.Application.Common;
using LabelPrint.Application.DTOs;

namespace LabelPrint.Application.Abstractions.Services;

/// <summary>
/// Local shift sign-in for workstation users.
/// </summary>
public interface IAuthService
{
    Task<Result<IReadOnlyList<UserListItemDto>>> ListUsersAsync(CancellationToken cancellationToken = default);

    Task<Result> SignInAsync(Guid userId, string? pin, CancellationToken cancellationToken = default);

    void SignOut();
}
