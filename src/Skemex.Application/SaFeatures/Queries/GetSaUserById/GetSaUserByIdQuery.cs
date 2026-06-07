using Skemex.Application.Features.Abstractions;

namespace Skemex.Application.SaFeatures.Queries.GetSaUserById;

public sealed class GetSaUserByIdQuery : IQuery<SaUserDto>
{
    public Guid UserId { get; init; }
}
