using MediatR;
using PasswordManager.Shared.Common.Result;

namespace PasswordManager.Shared.Core.Message;

/// <summary>
/// Handler interface for commands that don't return a response.
/// Returns a Result to indicate success or failure.
/// </summary>
/// <typeparam name="TCommand">The type of command to handle</typeparam>
public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand, Result>
    where TCommand : ICommand
{
}

/// <summary>
/// Handler interface for commands that return a response.
/// Returns a Result containing the response or error information.
/// </summary>
/// <typeparam name="TCommand">The type of command to handle</typeparam>
/// <typeparam name="TResponse">The type of response returned by the command</typeparam>
public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse>
{
}

