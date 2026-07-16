using UnityEngine;

public enum RoleItemCategory : byte
{
    None = 0,

    // Основной предмет роли.
    // Например: книга инструктора, ключ механика, планшет оператора.
    PrimaryRole = 1,

    // Будущие дополнительные предметы подроли.
    // Например: шуруповёрт, сканер, дрель, тестер проводов.
    SubRole = 2
}

public class RoleItem : MonoBehaviour
{
    [Header("Role")]
    [SerializeField] private PlayerRole role = PlayerRole.None;

    [Header("Category")]
    [SerializeField] private RoleItemCategory category = RoleItemCategory.PrimaryRole;

    [Header("Display")]
    [SerializeField] private string roleDisplayName = "Role Item";

    public PlayerRole Role => role;
    public RoleItemCategory Category => category;
    public string RoleDisplayName => roleDisplayName;

    public bool IsRoleItem => role != PlayerRole.None && category != RoleItemCategory.None;
    public bool IsPrimaryRoleItem => role != PlayerRole.None && category == RoleItemCategory.PrimaryRole;
    public bool IsSubRoleItem => role != PlayerRole.None && category == RoleItemCategory.SubRole;
}