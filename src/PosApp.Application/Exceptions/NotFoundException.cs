namespace PosApp.Application.Exceptions;

public sealed class NotFoundException : Exception
{
    public NotFoundException(string resourceName, string identifier)
        : base($"{resourceName} with identifier '{identifier}' was not found.")
    {
        ResourceName = resourceName;
        Identifier = identifier;
    }

    public string ResourceName { get; }

    public string Identifier { get; }
}
