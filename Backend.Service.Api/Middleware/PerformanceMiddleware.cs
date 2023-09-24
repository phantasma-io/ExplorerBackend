using System.Diagnostics;
using System.Threading.Tasks;
using Backend.Service.Api.Metrics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Backend.Service.Api.Middleware;

public class PerformanceMiddleware
{
    private readonly ILogger<PerformanceMiddleware> _logger;
    private readonly IEndpointMetrics _metrics;
    private readonly RequestDelegate _next;

    public PerformanceMiddleware(
        RequestDelegate next,
        ILogger<PerformanceMiddleware> logger,
        IEndpointMetrics metrics
    )
    {
        _next = next;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task Invoke(
        HttpContext httpContext
    )
    {
        Stopwatch timer = Stopwatch.StartNew();

        await _next(httpContext);

        timer.Stop();

        HttpRequest request = httpContext.Request;

        if (httpContext.Response.StatusCode >= 400 ||
            !request.Path.HasValue ||
            request.Path.StartsWithSegments("/swagger") ||
            request.Path.StartsWithSegments("/swagger-internal"))
        {
            // Ignore server errors, empty path and Swagger endpoints

            return;
        }

        string path = request.Path.Value!.ToLowerInvariant();

        if (path.Length > 1 && path.EndsWith("/"))
        {
            // Strip trailing slash for better grouping
            path = path.TrimEnd('/');
        }

        if (Settings.Default.PerformanceMetrics.CountsEnabled)
        {
            await _metrics.Count(path);
        }

        if (Settings.Default.PerformanceMetrics.AveragesEnabled)
        {
            await _metrics.Average(path, timer.ElapsedMilliseconds);
        }

        if (timer.ElapsedMilliseconds <= Settings.Default.PerformanceMetrics.LongRunningRequestThreshold)
        {
            return;
        }

        _logger.LogWarning("Long Running Request: Duration: {Duration}ms; Path: {Path}; Query: {@Query}",
            timer.ElapsedMilliseconds, path, request.Query);
    }
}
