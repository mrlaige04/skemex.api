using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Skemex.Application.Configuration;
using Skemex.Application.Models.Ai;
using Skemex.Application.Services.Ai;

namespace Skemex.Infrastructure.Services.Ai;

public sealed class AiChatService(
    IChatClient chatClient,
    IOptions<AiOptions> aiOptions) : IAiChatService
{
    public async Task<AiChatResult> CompleteAsync(
        AiChatRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SystemPrompt);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.UserPrompt);

        var model = string.IsNullOrWhiteSpace(request.Model)
            ? aiOptions.Value.DefaultModel
            : request.Model.Trim();

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, request.SystemPrompt),
            new(ChatRole.User, request.UserPrompt),
        };

        var options = new ChatOptions { ModelId = model };
        var response = await chatClient
            .GetResponseAsync(messages, options, cancellationToken)
            .ConfigureAwait(false);

        return new AiChatResult { Text = response.Text ?? string.Empty };
    }
}
