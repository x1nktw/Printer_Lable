using LabelPrint.Domain.Common;
using LabelPrint.Domain.Enums;

namespace LabelPrint.Domain.Entities;

/// <summary>
/// Local application user (single-workstation MVP).
/// </summary>
public class User : EntityBase
{
    /// <summary>Display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Access role.</summary>
    public UserRole Role { get; set; } = UserRole.Operator;

    /// <summary>Optional PIN hash for shift login; null means selection without PIN.</summary>
    public string? PinHash { get; set; }

    /// <summary>Whether the user can sign in.</summary>
    public bool IsActive { get; set; } = true;
}
