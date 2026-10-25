using Skemex.Application.Features.Abstractions;
using Skemex.Application.Models.Notifications;

namespace Skemex.Application.Features.Queries.Notifications.GetUnreadNotificationCount;

public sealed class GetUnreadNotificationCountQuery : IQuery<UnreadNotificationCountDto>;
