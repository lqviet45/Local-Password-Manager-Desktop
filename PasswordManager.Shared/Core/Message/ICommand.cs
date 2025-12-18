using MediatR;
using PasswordManager.Shared.Common.Result;

namespace PasswordManager.Shared.Core.Message;

/// <summary>
/// Marker interface for commands that don't return a response.
/// Commands represent operations that change the state of the system.
/// Returns a Result to indicate success or failure.
/// </summary>
public interface ICommand : IRequest<Result>
{
}

/// <summary>
/// Marker interface for commands that return a response.
/// Commands represent operations that change the state of the system.
/// Returns a Result containing the response or error information.
/// </summary>
/// <typeparam name="TResponse">The type of response returned by the command</typeparam>
public interface ICommand<TResponse> : IRequest<Result<TResponse>>
{
}

