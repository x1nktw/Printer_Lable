using LabelPrint.Application.Abstractions;
using LabelPrint.Domain.Enums;

namespace LabelPrint.Application.Services;

/// <summary>
/// In-memory signed-in user state for the workstation session.
/// </summary>
public sealed class UserSession : IUserSession
{
    public Guid? CurrentUserId { get; private set; }

    public string? CurrentUserName { get; private set; }

    public UserRole? CurrentUserRole { get; private set; }

    public bool IsSignedIn => CurrentUserId is not null;

    internal void SetUser(Guid userId, string name, UserRole role)
    {
        CurrentUserId = userId;
        CurrentUserName = name;
        CurrentUserRole = role;
    }

    internal void Clear()
    {
        CurrentUserId = null;
        CurrentUserName = null;
        CurrentUserRole = null;
    }
}
