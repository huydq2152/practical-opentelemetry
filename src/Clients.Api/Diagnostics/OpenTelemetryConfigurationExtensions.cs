using System.Reflection;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Clients.Api.Diagnostics;

public static class OpenTelemetryConfigurationExtensions
{
    public static WebApplicationBuilder AddOpenTelemetry(this WebApplicationBuilder builder)
    {
        const string serviceName = "Clients.Api";

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resourceBuilder => resourceBuilder
                .AddService(serviceName)
                .AddAttributes(new[]
                {
                    new KeyValuePair<string, object>("service.version",
                        Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown")
                })
            )
            .WithTracing(providerBuilder => providerBuilder
                .AddAspNetCoreInstrumentation()
                .AddGrpcClientInstrumentation()
                .AddHttpClientInstrumentation(options => { options.RecordException = true; })
                .AddNpgsql()
                .AddRedisInstrumentation()
                //.AddConsoleExporter()
                .AddOtlpExporter(options => { options.Endpoint = new Uri("http://jaeger:4317"); }) // Jaeger support receive tracing data directly via OTLP
            )
            .WithMetrics(providerBuilder => providerBuilder
                .AddMeter(ApplicationDiagnostics.Meter.Name)
                //.AddConsoleExporter()
                .AddPrometheusExporter() // Prometheus use pull model to scrape metrics, use this exporter to create an endpoint for Prometheus to scrape data
            );

        return builder;
    }
}