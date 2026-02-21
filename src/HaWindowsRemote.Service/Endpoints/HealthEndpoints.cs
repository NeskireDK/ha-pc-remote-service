using HaWindowsRemote.Service.Models;

namespace HaWindowsRemote.Service.Endpoints;

public static class HealthEndpoints
{
    public static RouteGroupBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api");

        group.MapGet("/health", () =>
        {
            var response = ApiResponse.Ok(new HealthResponse { Status = "ok" });
            return Results.Json(response, AppJsonContext.Default.ApiResponseHealthResponse);
        });

        return group;
    }
}
