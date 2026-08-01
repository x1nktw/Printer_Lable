using LabelPrint.Domain.Enums;

namespace LabelPrint.Application.Abstractions;

/// <summary>
/// Holds the signed-in workstation user for the current application session.
/// </summary>
public interface IUserSession
{
    Guid? CurrentUserId { get; }

    string? CurrentUserName { get; }

    UserRole? CurrentUserRole { get; }

    bool IsSignedIn { get; }
}
