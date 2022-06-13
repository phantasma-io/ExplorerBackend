using System;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Phantasma.Domain;

namespace GhostDevs.Service.Api.Middleware;

public class ErrorLoggingMiddleware
{
    private readonly ILogger<ErrorLoggingMiddleware> _logger;
    private readonly RequestDelegate _next;


    public ErrorLoggingMiddleware(RequestDelegate next, ILogger<ErrorLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }


    public async Task Invoke(HttpContext httpContext)
    {
        var path = httpContext.Request.Path;

        try
        {
            await _next(httpContext);
        }
        catch ( Exception e ) when ( e is ApiParameterException or ApiUnexpectedException )
        {
            // If there is no inner exception, it is likely just a field validation error so we won't log it
            if ( e.InnerException != null )
                _logger.LogError(e, "{Type} exception caught: {Path}", e.GetType().ToString(), path);

            if ( e is ApiParameterException )
                _logger.LogWarning(e, "{Type} exception caught: {Path}", e.GetType().ToString(), path);

            var body = JsonSerializer.Serialize(
                new ErrorResult {error = e.InnerException != null ? e.ToString() : e.Message},
                new JsonSerializerOptions {IncludeFields = true});
            var response = Encoding.UTF8.GetBytes(body);
            httpContext.Response.ContentType = "application/json; charset=utf-8";
            httpContext.Response.StatusCode = e is ApiParameterException
                ? ( int ) HttpStatusCode.BadRequest
                : ( int ) HttpStatusCode.InternalServerError;
            await httpContext.Response.Body.WriteAsync(response);
        }
        catch ( Exception e )
        {
            _logger.LogCritical(e, "Unexpected exception caught: {Path}", path);

            throw;
        }
    }
}
