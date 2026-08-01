namespace Skemex.Infrastructure.Services;

/// <summary>
/// AsyncLocal ambient identity for background jobs (Hangfire) where HttpContext is unavailable.
/// </summary>
public static class AmbientUserContext
{
    private static readonly AsyncLocal<Guid?> TenantIdLocal = new();
    private static readonly AsyncLocal<Guid?> UserIdLocal = new();

    public static Guid? TenantId => TenantIdLocal.Value;
    public static Guid? UserId => UserIdLocal.Value;

    public static IDisposable Use(Guid tenantId, Guid userId) =>
        new Scope(tenantId, userId);

    private sealed class Scope : IDisposable
    {
        private readonly Guid? _previousTenantId;
        private readonly Guid? _previousUserId;
        private bool _disposed;

        public Scope(Guid tenantId, Guid userId)
        {
            _previousTenantId = TenantIdLocal.Value;
            _previousUserId = UserIdLocal.Value;
            TenantIdLocal.Value = tenantId;
            UserIdLocal.Value = userId;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            TenantIdLocal.Value = _previousTenantId;
            UserIdLocal.Value = _previousUserId;
            _disposed = true;
        }
    }
}
