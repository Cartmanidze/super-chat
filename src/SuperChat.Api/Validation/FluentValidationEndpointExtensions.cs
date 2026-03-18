using FluentValidation;
using FluentValidation.Results;

namespace SuperChat.Api.Validation;

internal static class FluentValidationEndpointExtensions
{
    public static RouteHandlerBuilder ValidateRequest<TRequest>(this RouteHandlerBuilder builder)
    {
        return builder.AddEndpointFilterFactory((factoryContext, next) =>
        {
            var requestParameterIndex = Array.FindIndex(
                factoryContext.MethodInfo.GetParameters(),
                parameter => parameter.ParameterType == typeof(TRequest));

            if (requestParameterIndex < 0)
            {
                return next;
            }

            return async invocationContext =>
            {
                var validator = invocationContext.HttpContext.RequestServices.GetService<IValidator<TRequest>>();
                if (validator is null)
                {
                    return await next(invocationContext);
                }

                var request = invocationContext.GetArgument<TRequest>(requestParameterIndex);
                var validationResult = await validator.ValidateAsync(request, invocationContext.HttpContext.RequestAborted);
                return validationResult.IsValid
                    ? await next(invocationContext)
                    : validationResult.ToValidationProblem();
            };
        });
    }

    public static IResult ToValidationProblem(this ValidationResult validationResult)
    {
        return Results.ValidationProblem(validationResult.Errors
            .GroupBy(error => ToCamelCase(error.PropertyName))
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(error => error.ErrorMessage)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()));
    }

    private static string ToCamelCase(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return string.Empty;
        }

        return propertyName.Length == 1
            ? propertyName.ToLowerInvariant()
            : char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
    }
}
