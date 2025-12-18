namespace PasswordManager.Application.Common.Exceptions;



/// <summary>

/// Exception thrown when an entity cannot be found.

/// </summary>

public class NotFoundException : Exception

{

    public NotFoundException(string name, object key)

        : base($"{name} with ID {key} was not found")

    {

    }

}

