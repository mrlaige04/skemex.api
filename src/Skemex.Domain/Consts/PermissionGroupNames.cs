namespace Skemex.Domain.Consts;

public static class PermissionGroupNames
{
    public const string Ai = "AI";
    public const string Documents = "Documents";
    public const string Projects = "Projects";
    public const string SettingsColumns = "Settings — Columns";
    public const string SettingsSpecializations = "Settings — Specializations";
    public const string SettingsRoles = "Settings — Roles";
    public const string Tasks = "Tasks";
    public const string Users = "Users";

    public static IReadOnlyList<(string Group, IReadOnlyList<string> Permissions)> GetCatalog() =>
    [
        (Ai, [
            PermissionNames.AiChat,
            PermissionNames.AiExecute,
            PermissionNames.AiViewUsage,
        ]),
        (Documents, [
            PermissionNames.DocumentsUpload,
            PermissionNames.DocumentsView,
            PermissionNames.DocumentsEdit,
            PermissionNames.DocumentsDelete,
        ]),
        (Projects, [
            PermissionNames.ProjectsCreate,
            PermissionNames.ProjectsEdit,
            PermissionNames.ProjectsView,
            PermissionNames.ProjectsDelete,
            PermissionNames.ProjectsViewAll,
        ]),
        (SettingsColumns, [
            PermissionNames.SettingsColumnsView,
            PermissionNames.SettingsColumnsCreate,
            PermissionNames.SettingsColumnsEdit,
            PermissionNames.SettingsColumnsDelete,
        ]),
        (SettingsSpecializations, [
            PermissionNames.SettingsSpecializationsView,
            PermissionNames.SettingsSpecializationsCreate,
            PermissionNames.SettingsSpecializationsEdit,
            PermissionNames.SettingsSpecializationsDelete,
        ]),
        (SettingsRoles, [
            PermissionNames.SettingsRolesView,
            PermissionNames.SettingsRolesCreate,
            PermissionNames.SettingsRolesEdit,
            PermissionNames.SettingsRolesDelete,
        ]),
        (Tasks, [
            PermissionNames.TasksCreate,
            PermissionNames.TasksView,
            PermissionNames.TasksEdit,
            PermissionNames.TasksDelete,
        ]),
        (Users, [
            PermissionNames.UsersCreate,
            PermissionNames.UsersEdit,
            PermissionNames.UsersView,
            PermissionNames.UsersDelete,
        ]),
    ];
}
