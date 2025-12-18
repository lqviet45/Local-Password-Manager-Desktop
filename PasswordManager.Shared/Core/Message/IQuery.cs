using MediatR;
using PasswordManager.Shared.Common.Result;

namespace PasswordManager.Shared.Core.Message;

/// <summary>
/// Marker interface for queries that return a response.
/// Queries represent read operations that don't change the state of the system.
/// Returns a Result containing the response or error information.
/// </summary>
/// <typeparam name="TResponse">The type of response returned by the query</typeparam>
public interface IQuery<TResponse> : IRequest<Result<TResponse>>
{
}

