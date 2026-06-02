using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;

namespace Skemex.Web.Infrastructure;

public class SkemexExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is ValidationException validationException)
        {
            await HandleValidationException(httpContext, validationException).ConfigureAwait(false);
            return true;
        }

        if (exception is UnauthorizedAccessException)
        {
            await HandleUnauthorizedException(httpContext, exception).ConfigureAwait(false);
            return true;
        }

        return false;
    }

    private static async Task HandleValidationException(HttpContext httpContext, ValidationException exception)
    {
        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        await httpContext.Response
            .WriteAsJsonAsync(
                new
                {
                    Detail = string.Join("\n", exception.Errors.Select(error => error.ErrorMessage)),
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                },
                cancellationToken: httpContext.RequestAborted)
            .ConfigureAwait(false);
    }

    private static Task HandleUnauthorizedException(HttpContext httpContext, Exception ex)
    {
        httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }
}
