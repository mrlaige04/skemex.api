using ErrorOr;

namespace Skemex.Application.Features.Abstractions;

public interface IQuery<TQuery>;

public interface IQueryHandler<in TQuery, TResponse> where TQuery : IQuery<TResponse>
{
    Task<ErrorOr<TResponse>> Handle(TQuery query, CancellationToken cancellationToken = default);
}