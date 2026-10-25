using Skemex.Application.Services.Ai.Agents;
using Skemex.Application.Services.Ai.Chat;
using Skemex.Application.Services.Ai.Completions;
using Skemex.Application.Services.Ai.Embeddings;
using Skemex.Application.Services.Ai.Providers;
using Skemex.Application.Services.Ai.Rag;
using Skemex.Application.Services.Ai.Usage;
using Skemex.Application.Services.Common;
using Skemex.Application.Services.Documents;
using Skemex.Application.Services.Email;
using Skemex.Application.Services.Encryption;
using Skemex.Application.Services.Notifications;
using Skemex.Application.Services.Projects;
using Skemex.Application.Services.Storage;
using Skemex.Application.Services.Users;
using Skemex.Application.Models.Storage;

namespace Skemex.Infrastructure.Storage;

internal static class StorageBucketSegments
{
    public static string LocalFolderName(StorageBucketKind kind) =>
        kind switch
        {
            StorageBucketKind.Branding => "branding",
            StorageBucketKind.Files => "files",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
}
