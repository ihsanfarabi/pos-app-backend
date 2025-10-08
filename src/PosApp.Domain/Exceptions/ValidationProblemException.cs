using System.Collections.Generic;

namespace PosApp.Domain.Exceptions;

public sealed class ValidationProblemException : DomainException
{
    public sealed record FieldError(string Code, string Message);

    public IReadOnlyDictionary<string, IReadOnlyList<FieldError>> Errors { get; }

    public ValidationProblemException(
        string message,
        IReadOnlyDictionary<string, IReadOnlyList<FieldError>> errors)
        : base(message)
    {
        Errors = errors;
    }
}


