using ErrorOr;
using FluentValidation;
using Skemex.Application.Features.Abstractions;

namespace Skemex.Application.Features.Behaviours;

public class ValidationBehaviour<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators) 
    : IBehaviour<TRequest, TResponse> where TRequest: notnull
{
    public async Task<ErrorOr<TResponse>> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, 
        CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next();
        }

        var ctx = new ValidationContext<TRequest>(request);
        var validationResults = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(ctx, cancellationToken)));

        var failures = validationResults
            .Where(v => v.Errors.Count != 0)
            .SelectMany(v => v.Errors)
            .ToList();

        if (failures.Count != 0)
        {
            throw new ValidationException(failures);
        }

        return await next();
    }
}
