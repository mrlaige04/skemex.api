using System.Linq.Expressions;
using ErrorOr;
using Skemex.Application.Features.Abstractions;
using Skemex.Application.SaModels.SaAgentTools;
using Skemex.Domain.Entities.Ai;
using Skemex.Domain.Repositories.Abstractions;
using Skemex.Domain.Services;

namespace Skemex.Application.SaFeatures.Queries.SaAgentTools.GetSaAgentTools;

public sealed class GetSaAgentToolsQueryHandler(
    ICurrentUser currentUser,
    IBaseRepository<AgentTool> agentToolRepository)
    : IQueryHandler<GetSaAgentToolsQuery, IReadOnlyList<SaAgentToolSummaryDto>>
{
    public async Task<ErrorOr<IReadOnlyList<SaAgentToolSummaryDto>>> Handle(
        GetSaAgentToolsQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.IsSuperAdmin())
        {
            return Error.Forbidden("SuperAdmin.Required", "Platform administrator access is required.");
        }

        var term = request.Search?.Trim().ToLowerInvariant() ?? string.Empty;
        var hasSearch = term.Length > 0;

        Expression<Func<AgentTool, bool>> filter = tool =>
            !hasSearch
            || tool.SystemName.ToLower().Contains(term)
            || tool.Description.ToLower().Contains(term);

        var tools = await agentToolRepository.GetAllAsync(
            filter: filter,
            include: query => query.OrderBy(tool => tool.SystemName),
            cancellationToken: cancellationToken);

        return tools
            .Select(tool => new SaAgentToolSummaryDto
            {
                Id = tool.Id,
                SystemName = tool.SystemName,
                Description = tool.Description,
                CreatedAt = tool.CreatedAt,
                UpdatedAt = tool.UpdatedAt,
            })
            .ToList();
    }
}
