namespace PasswordManager.Application.Common.Exceptions;



/// <summary>

/// Exception thrown when a user attempts an action they are not authorized to perform.

/// </summary>

public class ForbiddenAccessException : Exception

{

    public ForbiddenAccessException()

        : base("You do not have permission to perform this action.")

    {

    }

}

