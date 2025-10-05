using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

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
            throw new ValidationException(failures);
        }

        return await next();
    }
}


