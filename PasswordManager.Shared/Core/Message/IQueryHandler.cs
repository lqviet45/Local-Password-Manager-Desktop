using MediatR;
using PasswordManager.Shared.Common.Result;

namespace PasswordManager.Shared.Core.Message;

/// <summary>
/// Handler interface for queries that return a response.
/// Returns a Result containing the response or error information.
/// </summary>
/// <typeparam name="TQuery">The type of query to handle</typeparam>
/// <typeparam name="TResponse">The type of response returned by the query</typeparam>
public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>
{
}

