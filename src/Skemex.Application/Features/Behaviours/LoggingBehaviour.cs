using ErrorOr;
using Microsoft.Extensions.Logging;
using Skemex.Application.Features.Abstractions;

namespace Skemex.Application.Features.Behaviours;

public class LoggingBehaviour<TRequest, TResponse>(
    ILogger<LoggingBehaviour<TRequest, TResponse>> logger) : IBehaviour<TRequest, TResponse>
{
    public async Task<ErrorOr<TResponse>> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling request {RequestType}", typeof(TRequest).Name);

        var result = await next();

        logger.LogInformation("Handled {RequestType}", typeof(TRequest).Name);

        return result;
    }
}