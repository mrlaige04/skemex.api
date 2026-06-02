using ErrorOr;

namespace Skemex.Application.Features.Abstractions;

public class Sender(IServiceProvider serviceProvider) : ISender
{
    public Task<ErrorOr<Success>> Send(ICommand command, CancellationToken cancellationToken = default)
    {
        return SendInternal<ICommand, Success>(command, cancellationToken);
    }

    public Task<ErrorOr<TResponse>> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
    {
        return SendInternal<ICommand<TResponse>, TResponse>(command, cancellationToken);
    }

    public Task<ErrorOr<TResponse>> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
    {
        return SendInternal<IQuery<TResponse>, TResponse>(query, cancellationToken);
    }

    private async Task<ErrorOr<TResponse>> SendInternal<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
    {
        var handlerType = request switch
        {
            ICommand => typeof(ICommandHandler<>).MakeGenericType(request.GetType()),
            ICommand<TResponse> => typeof(ICommandHandler<,>).MakeGenericType(request.GetType(), typeof(TResponse)),
            IQuery<TResponse> => typeof(IQueryHandler<,>).MakeGenericType(request.GetType(), typeof(TResponse)),
            _ => throw new InvalidOperationException($"Unknown request type {typeof(TRequest)}")
        };

        dynamic? handler = serviceProvider.GetService(handlerType);

        if (handler is null)
        {
            throw new InvalidOperationException($"Handler not found for type {handlerType}");
        }

        var behaviorType = typeof(IBehaviour<,>).MakeGenericType(request.GetType(), typeof(TResponse));
        var behaviors = (IEnumerable<object>)(serviceProvider.GetService(typeof(IEnumerable<>).MakeGenericType(behaviorType)) ?? Array.Empty<object>());

        RequestHandlerDelegate<TResponse> handlerDelegate = () => handler.Handle((dynamic)request, cancellationToken);
        foreach (dynamic behavior in behaviors.Reverse())
        {
            var next = handlerDelegate;
            handlerDelegate = () => behavior.Handle((dynamic)request, next, cancellationToken);
        }

        return await handlerDelegate();
    }
}