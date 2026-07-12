using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Skemex.Application.Services.Projects;
using Skemex.Infrastructure.Data;

namespace Skemex.Infrastructure.Services;

public sealed class ProjectTaskCodeAllocator(SkemexDbContext dbContext) : IProjectTaskCodeAllocator
{
    public async Task<int> AllocateNextNumberAsync(
        Guid tenantId,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        AttachTransaction(command);

        command.CommandText = """
            INSERT INTO project_task_counters ("Id", "TenantId", "ProjectId", "NextNumber", "CreatedAt")
            VALUES (@id, @tenantId, @projectId, 2, @createdAt)
            ON CONFLICT ("ProjectId")
            DO UPDATE SET
                "NextNumber" = project_task_counters."NextNumber" + 1,
                "UpdatedAt" = @updatedAt
            RETURNING project_task_counters."NextNumber" - 1
            """;

        AddParameter(command, "@id", Guid.NewGuid());
        AddParameter(command, "@tenantId", tenantId);
        AddParameter(command, "@projectId", projectId);
        AddParameter(command, "@createdAt", DateTime.UtcNow);
        AddParameter(command, "@updatedAt", DateTime.UtcNow);

        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        if (scalar is null or DBNull)
        {
            throw new InvalidOperationException("Failed to allocate a project task number.");
        }

        return Convert.ToInt32(scalar);
    }

    private void AttachTransaction(DbCommand command)
    {
        var transaction = dbContext.Database.CurrentTransaction;
        if (transaction is not null)
        {
            command.Transaction = transaction.GetDbTransaction();
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
