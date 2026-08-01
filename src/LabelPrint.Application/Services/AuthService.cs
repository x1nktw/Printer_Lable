using System.Security.Cryptography;
using System.Text;
using LabelPrint.Application.Abstractions;
using LabelPrint.Application.Abstractions.Repositories;
using LabelPrint.Application.Abstractions.Services;
using LabelPrint.Application.Common;
using LabelPrint.Application.DTOs;
using Microsoft.Extensions.Logging;

namespace LabelPrint.Application.Services;

/// <summary>
/// Local workstation sign-in against seeded users.
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserSession _session;
    private readonly ILogger<AuthService> _logger;

    public AuthService(IUnitOfWork unitOfWork, IUserSession session, ILogger<AuthService> logger)
    {
        _unitOfWork = unitOfWork;
        _session = (UserSession)session;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<UserListItemDto>>> ListUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await _unitOfWork.Users.GetActiveUsersAsync(cancellationToken);
        var dtos = users.Select(u => new UserListItemDto
        {
            Id = u.Id,
            Name = u.Name,
            Role = u.Role,
            RequiresPin = !string.IsNullOrEmpty(u.PinHash)
        }).ToList();

        return Result.Success<IReadOnlyList<UserListItemDto>>(dtos);
    }

    /// <inheritdoc />
    public async Task<Result> SignInAsync(Guid userId, string? pin, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Result.Failure("User not found.");
        }

        if (!string.IsNullOrEmpty(user.PinHash))
        {
            if (string.IsNullOrEmpty(pin))
            {
                return Result.Failure("PIN is required.");
            }

            var hash = HashPin(pin);
            if (!string.Equals(hash, user.PinHash, StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure("Invalid PIN.");
            }
        }

        _session.SetUser(user.Id, user.Name, user.Role);
        _logger.LogInformation("User {UserId} signed in as {Role}", user.Id, user.Role);
        return Result.Success();
    }

    /// <inheritdoc />
    public void SignOut()
    {
        if (_session.IsSignedIn)
        {
            _logger.LogInformation("User {UserId} signed out", _session.CurrentUserId);
        }

        _session.Clear();
    }

    public static string HashPin(string pin)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(pin));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
