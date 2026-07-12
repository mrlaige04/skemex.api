using ErrorOr;
using Microsoft.AspNetCore.Identity;

namespace Skemex.Application.Extensions;

public static class IdentityExtensions
{
    extension(IdentityResult error)
    {
        public ErrorOr<Success> ToErrorOr(string? errorCode, string? errorDescription)
        {
            var errors = error.Errors
                .ToDictionary(e => e.Code, object (e) => e.Description);

            var code = string.IsNullOrEmpty(errorCode)
                ? "Something went wrong"
                : errorCode;

            var description = string.IsNullOrEmpty(errorDescription)
                ? "Something went wrong"
                : errorDescription;

            return Error.Failure(code, description, errors);
        }

        public ErrorOr<T> ToErrorOr<T>(string? errorCode, string? errorDescription)
        {
            var errors = error.Errors
                .ToDictionary(e => e.Code, object (e) => e.Description);

            var code = string.IsNullOrEmpty(errorCode)
                ? "Something went wrong"
                : errorCode;

            var description = string.IsNullOrEmpty(errorDescription)
                ? "Something went wrong"
                : errorDescription;

            return Error.Failure(code, description, errors);
        }
    }
}