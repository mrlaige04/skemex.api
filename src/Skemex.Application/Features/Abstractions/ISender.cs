using ErrorOr;

namespace Skemex.Application.Features.Abstractions;

public interface ISender
{
    Task<ErrorOr<Success>> Send(ICommand command, CancellationToken cancellationToken = default);
    Task<ErrorOr<TResponse>> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default);
    Task<ErrorOr<TResponse>> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default);
}