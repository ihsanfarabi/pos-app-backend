namespace PosApp.Domain.Exceptions;

public class DomainException : Exception
{
    public DomainException(string message, string? propertyName = null)
        : base(message)
    {
        PropertyName = propertyName;
    }

    public string? PropertyName { get; }
}
