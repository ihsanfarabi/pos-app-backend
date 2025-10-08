using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using PosApp.Domain.Exceptions;
using System.Collections.Generic;

namespace PosApp.Application.Behaviors;

public sealed class ValidatorBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators,
    ILogger<ValidatorBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return await next();
        }

        var typeName = typeof(TRequest).Name;
        logger.LogInformation("Validating request {RequestName}", typeName);

        var context = new ValidationContext<TRequest>(request);
        var validationResults = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken)));
        var failures = validationResults.SelectMany(r => r.Errors).Where(f => f is not null).ToList();

        if (failures.Count != 0)
        {
            logger.LogWarning("Validation errors - {RequestName} - Errors: {@ValidationErrors}", typeName, failures);

            var grouped = new Dictionary<string, List<ValidationProblemException.FieldError>>();
            foreach (var failure in failures)
            {
                var key = failure.PropertyName ?? string.Empty;
                if (!grouped.TryGetValue(key, out var list))
                {
                    list = new List<ValidationProblemException.FieldError>();
                    grouped[key] = list;
                }
                var code = string.IsNullOrWhiteSpace(failure.ErrorCode) ? "Validation" : failure.ErrorCode;
                list.Add(new ValidationProblemException.FieldError(code, failure.ErrorMessage));
            }

            var readonlyGrouped = grouped.ToDictionary(
                kvp => kvp.Key,
                kvp => (IReadOnlyList<ValidationProblemException.FieldError>)kvp.Value);

            throw new ValidationProblemException(
                $"Command Validation Errors for type {typeof(TRequest).Name}",
                readonlyGrouped);
        }

        return await next();
    }
}


