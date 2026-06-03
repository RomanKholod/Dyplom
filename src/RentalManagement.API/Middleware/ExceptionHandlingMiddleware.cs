using System.Net;
using System.Text.Json;
using FluentValidation;

namespace RentalManagement.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning("Validation error: {Errors}", ex.Errors);
            ctx.Response.StatusCode = (int)HttpStatusCode.UnprocessableEntity;
            ctx.Response.ContentType = "application/json";

            var errors = ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            await ctx.Response.WriteAsync(JsonSerializer.Serialize(new { errors }));
        }
        catch (UnauthorizedAccessException)
        {
            ctx.Response.StatusCode = (int)HttpStatusCode.Forbidden;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            ctx.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = "An unexpected error occurred",
                detail = ex.Message
            }));
        }
    }
}
