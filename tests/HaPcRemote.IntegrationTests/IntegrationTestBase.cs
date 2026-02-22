using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HaPcRemote.IntegrationTests.Models;
using Microsoft.Extensions.Configuration;

namespace HaPcRemote.IntegrationTests;

public abstract class IntegrationTestBase : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    protected HttpClient Client { get; }
    protected string BaseUrl { get; }
    protected string ApiKey { get; }

    protected IntegrationTestBase()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.IntegrationTests.json", optional: false)
            .AddEnvironmentVariables()
            .Build();

        BaseUrl = Environment.GetEnvironmentVariable("PCREMOTE_BASE_URL")
                  ?? config["ServiceBaseUrl"]
                  ?? throw new InvalidOperationException("ServiceBaseUrl not configured");

        ApiKey = Environment.GetEnvironmentVariable("PCREMOTE_API_KEY")
                 ?? config["ApiKey"]
                 ?? throw new InvalidOperationException("ApiKey not configured");

        Client = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        Client.DefaultRequestHeaders.Add("X-Api-Key", ApiKey);
    }

    protected HttpClient CreateClientWithoutApiKey()
    {
        return new HttpClient { BaseAddress = new Uri(BaseUrl) };
    }

    protected HttpClient CreateClientWithApiKey(string apiKey)
    {
        var client = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        return client;
    }

    protected async Task<ApiResponse<T>> GetAsync<T>(string path)
    {
        var response = await Client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<T>>(JsonOptions);
        return result ?? throw new InvalidOperationException($"Failed to deserialize response from {path}");
    }

    protected async Task<ApiResponse> PostAsync(string path)
    {
        var response = await Client.PostAsync(path, null);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ApiResponse>(JsonOptions);
        return result ?? throw new InvalidOperationException($"Failed to deserialize response from {path}");
    }

    protected async Task<HttpResponseMessage> GetRawAsync(string path, HttpClient? client = null)
    {
        return await (client ?? Client).GetAsync(path);
    }

    protected async Task<HttpResponseMessage> PostRawAsync(string path, HttpClient? client = null)
    {
        return await (client ?? Client).PostAsync(path, null);
    }

    protected static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response)
    {
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
    }

    public void Dispose()
    {
        Client.Dispose();
        GC.SuppressFinalize(this);
    }
}
