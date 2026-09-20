using Polly;
using Polly.Registry;
using Skemex.Application.Services.Ai;
using Skemex.Domain.Consts;

namespace Skemex.Infrastructure.Services.Ai;

public sealed class AiSemanticRetry(ResiliencePipelineProvider<string> pipelineProvider) : IAiSemanticRetry
{
    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> attempt,
        CancellationToken cancellationToken = default)
    {
        var pipeline = pipelineProvider.GetPipeline(AiResiliencePipelineNames.Semantic);
        return await pipeline
            .ExecuteAsync(
                async token => await attempt(token).ConfigureAwait(false),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
