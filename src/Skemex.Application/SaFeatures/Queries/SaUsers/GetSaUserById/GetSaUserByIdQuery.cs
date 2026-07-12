using Skemex.Application.Features.Abstractions;
using Skemex.Application.SaModels.SaUsers;

namespace Skemex.Application.SaFeatures.Queries.SaUsers.GetSaUserById;

public sealed class GetSaUserByIdQuery : IQuery<SaUserDto>
{
    public Guid UserId { get; init; }
}
