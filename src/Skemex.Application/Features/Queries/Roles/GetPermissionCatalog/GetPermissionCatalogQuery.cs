using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Roles;

namespace Skemex.Application.Features.Queries.Roles.GetPermissionCatalog;

public sealed class GetPermissionCatalogQuery : IQuery<IReadOnlyList<PermissionGroupDto>>;
