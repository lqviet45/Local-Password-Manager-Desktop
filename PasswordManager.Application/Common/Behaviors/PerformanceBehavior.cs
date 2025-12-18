using System.Diagnostics;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;

namespace PasswordManager.Application.Common.Behaviors;

/// <summary>
/// Pipeline behavior that measures request execution time and logs a warning
/// if execution exceeds 500ms. Includes request details in the log.
/// </summary>
public class PerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<PerformanceBehavior<TRequest, TResponse>> _logger;
    private const int WarningThresholdMs = 500;

    public PerformanceBehavior(ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await next();
            stopwatch.Stop();

            var elapsedMilliseconds = stopwatch.ElapsedMilliseconds;

            if (elapsedMilliseconds > WarningThresholdMs)
            {
                var requestDetails = GetRequestDetails(request);

                _logger.LogWarning(
                    "Performance warning: {RequestName} took {ElapsedMilliseconds}ms (threshold: {ThresholdMs}ms). Request details: {RequestDetails}",
                    requestName,
                    elapsedMilliseconds,
                    WarningThresholdMs,
                    requestDetails);
            }

            return response;
        }
        catch (Exception)
        {
            stopwatch.Stop();
            throw;
        }
    }

    private static string GetRequestDetails(TRequest request)
    {
        try
        {
            // Use JSON serialization to get request details, but limit size
            var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                WriteIndented = false,
                MaxDepth = 3
            });

            // Limit the length to avoid huge log entries
            const int maxLength = 500;
            return json.Length > maxLength 
                ? json[..maxLength] + "..." 
                : json;
        }
        catch
        {
            // If serialization fails, return type name
            return request?.GetType().Name ?? "Unknown";
        }
    }
}

