using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

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

    private static Dictionary<string, string[]> ToDictionary(this ValidationResult result)
    {
        return result.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(
                group => JsonNamingPolicy.CamelCase.ConvertName(group.Key),
                group => group.Select(error => error.ErrorMessage).ToArray());
    }
}
