using ErrorOr;

namespace Skemex.Application.Features.Abstractions;

public interface IBehaviour<in TRequest, TResponse>
{
    Task<ErrorOr<TResponse>> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}

public delegate Task<ErrorOr<TResponse>> RequestHandlerDelegate<TResponse>();