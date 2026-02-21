namespace HaPcRemote.Service.Models;

public sealed class ApiResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }

    public static ApiResponse Ok(string? message = null) => new() { Success = true, Message = message };
    public static ApiResponse Fail(string message) => new() { Success = false, Message = message };
    public static ApiResponse<T> Ok<T>(T data, string? message = null) => new() { Success = true, Data = data, Message = message };
}

public sealed class ApiResponse<T>
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public T? Data { get; init; }
}
