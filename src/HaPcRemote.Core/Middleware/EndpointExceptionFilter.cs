using HaPcRemote.Service.Models;

namespace HaPcRemote.Service.Middleware;

public class EndpointExceptionFilter(ILogger<EndpointExceptionFilter> logger) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (KeyNotFoundException ex)
        {
            return Results.Json(
                ApiResponse.Fail(ex.Message),
                AppJsonContext.Default.ApiResponse,
                statusCode: StatusCodes.Status404NotFound);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Method} {Path} failed",
                context.HttpContext.Request.Method,
                context.HttpContext.Request.Path);
            return Results.Json(
                ApiResponse.Fail("Internal server error"),
                AppJsonContext.Default.ApiResponse,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
