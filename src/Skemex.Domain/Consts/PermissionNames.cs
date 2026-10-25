namespace Skemex.Domain.Consts;

public static class PermissionNames
{
    // AI
    public const string AiChat = "ai:chat";
    public const string AiExecute = "ai:execute";
    public const string AiViewUsage = "ai:view_usage";

    // Documents
    public const string DocumentsUpload = "documents:upload";
    public const string DocumentsView = "documents:view";
    public const string DocumentsEdit = "documents:edit";
    public const string DocumentsDelete = "documents:delete";

    // Projects
    public const string ProjectsCreate = "projects:create";
    public const string ProjectsEdit = "projects:edit";
    public const string ProjectsView = "projects:view";
    public const string ProjectsDelete = "projects:delete";
    /// <summary>See all tenant projects without ProjectUser membership (typically Admin).</summary>
    public const string ProjectsViewAll = "projects:view_all";

    // Settings — Columns
    public const string SettingsColumnsView = "settings:columns_view";
    public const string SettingsColumnsCreate = "settings:columns_create";
    public const string SettingsColumnsEdit = "settings:columns_edit";
    public const string SettingsColumnsDelete = "settings:columns_delete";

    // Settings — Specializations
    public const string SettingsSpecializationsView = "settings:specializations_view";
    public const string SettingsSpecializationsCreate = "settings:specializations_create";
    public const string SettingsSpecializationsEdit = "settings:specializations_edit";
    public const string SettingsSpecializationsDelete = "settings:specializations_delete";

    // Settings — Roles
    public const string SettingsRolesView = "settings:roles_view";
    public const string SettingsRolesCreate = "settings:roles_create";
    public const string SettingsRolesEdit = "settings:roles_edit";
    public const string SettingsRolesDelete = "settings:roles_delete";

    // Tasks
    public const string TasksCreate = "tasks:create";
    public const string TasksView = "tasks:view";
    public const string TasksEdit = "tasks:edit";
    public const string TasksDelete = "tasks:delete";

    // Users
    public const string UsersCreate = "users:create";
    public const string UsersEdit = "users:edit";
    public const string UsersView = "users:view";
    public const string UsersDelete = "users:delete";

    public static IReadOnlyList<string> GetAll() =>
    [
        AiChat,
        AiExecute,
        AiViewUsage,
        DocumentsUpload,
        DocumentsView,
        DocumentsEdit,
        DocumentsDelete,
        ProjectsCreate,
        ProjectsEdit,
        ProjectsView,
        ProjectsDelete,
        ProjectsViewAll,
        SettingsColumnsView,
        SettingsColumnsCreate,
        SettingsColumnsEdit,
        SettingsColumnsDelete,
        SettingsSpecializationsView,
        SettingsSpecializationsCreate,
        SettingsSpecializationsEdit,
        SettingsSpecializationsDelete,
        SettingsRolesView,
        SettingsRolesCreate,
        SettingsRolesEdit,
        SettingsRolesDelete,
        TasksCreate,
        TasksView,
        TasksEdit,
        TasksDelete,
        UsersCreate,
        UsersEdit,
        UsersView,
        UsersDelete,
    ];

    /// <summary>
    /// Default Developer / QA grants: project membership–scoped work, no tenant settings surfaces.
    /// Includes <see cref="UsersView"/> but not user create/edit/delete.
    /// </summary>
    public static IReadOnlyList<string> GetDeveloperGrants() =>
        GetAll()
            .Where(p => !p.StartsWith("settings:", StringComparison.Ordinal)
                        && p is not (
                            ProjectsViewAll
                            or UsersCreate
                            or UsersEdit
                            or UsersDelete))
            .ToList();

    /// <summary>
    /// Manager: same as Developer plus full Users permissions.
    /// </summary>
    public static IReadOnlyList<string> GetManagerGrants() =>
        GetDeveloperGrants()
            .Concat([UsersCreate, UsersEdit, UsersView, UsersDelete])
            .Distinct(StringComparer.Ordinal)
            .ToList();
}
