// ─────────────────────────────────────────────────────────────────────────────
// ProgramExtensions.cs
// Extension methods to register the Python image service HttpClient cleanly
// in Program.cs / Startup.cs with Polly resilience policies.
//
// Usage in Program.cs:
//   builder.Services.AddImageSearchService(builder.Configuration);
// ─────────────────────────────────────────────────────────────────────────────

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using SmartKitchen.API.Services;

namespace SmartKitchen.API.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers ImageSearchService as a typed HttpClient pointing at the
    /// Python image-collection microservice.
    ///
    /// Configuration key (appsettings.json):
    ///   "ImageService:BaseUrl": "http://localhost:8000"
    /// </summary>
    public static IServiceCollection AddImageSearchService(
        this IServiceCollection services,
        IConfiguration          configuration)
    {
        var baseUrl = configuration["ImageService:BaseUrl"]
                      ?? "http://localhost:8000";

        services
            .AddHttpClient<IImageSearchService, ImageSearchService>(client =>
            {
                client.BaseAddress = new Uri(baseUrl);
                client.Timeout     = TimeSpan.FromSeconds(60);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
            })
            // Polly retry: 3 attempts with exponential back-off
            .AddPolicyHandler(GetRetryPolicy())
            // Polly circuit breaker: open after 5 failures in 30 s
            .AddPolicyHandler(GetCircuitBreakerPolicy());

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                3,
                attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))
            );

    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30)
            );
}
