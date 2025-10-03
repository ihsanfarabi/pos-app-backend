namespace PosApp.Application.Exceptions;

public sealed class ValidationException : Exception
{
    public ValidationException(string message, string? propertyName = null) : base(message)
    {
        PropertyName = propertyName;
    }

    public string? PropertyName { get; }
}
