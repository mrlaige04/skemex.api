using Skemex.Application.Features.Abstractions;
using Skemex.Infrastructure.Authentication.Models;

namespace Skemex.Infrastructure.Authentication.Session;

public sealed class GetCurrentUserSessionQuery : IQuery<CurrentUserResponse>;
