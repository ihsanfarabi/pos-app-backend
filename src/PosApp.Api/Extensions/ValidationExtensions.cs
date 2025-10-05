using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;

namespace PosApp.Api.Extensions;

public static class ValidationExtensions
{
    public static RouteHandlerBuilder WithValidator<TRequest>(this RouteHandlerBuilder builder) where TRequest : class
    {
        return builder.AddEndpointFilterFactory((context, next) =>
        {
            return async invocationContext =>
            {
                var validator = invocationContext.HttpContext.RequestServices.GetService<IValidator<TRequest>>();
                if (validator is null)
                {
                    return await next(invocationContext);
                }

                var model = invocationContext.Arguments.OfType<TRequest>().FirstOrDefault();
                if (model is null)
                {
                    return await next(invocationContext);
                }

                var validationResult = await validator.ValidateAsync(model, invocationContext.HttpContext.RequestAborted);
                if (!validationResult.IsValid)
                {
                    return Results.ValidationProblem(validationResult.ToDictionary(), statusCode: StatusCodes.Status400BadRequest);
                }

                return await next(invocationContext);
            };
        });
    }

    public static Dictionary<string, string[]> ToDictionary(this ValidationResult result)
    {
        return result.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(
                group => JsonNamingPolicy.CamelCase.ConvertName(group.Key),
                group => group.Select(error => error.ErrorMessage).ToArray());
    }

    public static Dictionary<string, string[]> ToDictionary(this IEnumerable<ValidationFailure> errors)
    {
        return errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(
                group => JsonNamingPolicy.CamelCase.ConvertName(group.Key),
                group => group.Select(error => error.ErrorMessage).ToArray());
    }
}
