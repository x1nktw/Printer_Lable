using LabelPrint.Domain.Enums;

namespace LabelPrint.Application.DTOs;

/// <summary>User entry for sign-in picker.</summary>
public sealed class UserListItemDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public UserRole Role { get; init; }

    public bool RequiresPin { get; init; }
}
