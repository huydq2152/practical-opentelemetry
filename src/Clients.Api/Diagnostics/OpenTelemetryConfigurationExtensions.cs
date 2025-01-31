﻿using System.Reflection;
using Infrastructure.RabbitMQ;
using Npgsql;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Clients.Api.Diagnostics;

public static class OpenTelemetryConfigurationExtensions
{
    public static WebApplicationBuilder AddOpenTelemetry(this WebApplicationBuilder builder)
    {
        const string serviceName = "Clients.Api";

        var otlpEndpoint = new Uri(builder.Configuration.GetValue<string>("OTLP_Endpoint")!);
        
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
                    .AddSource(RabbitMqDiagnostics.ActivitySourceName)
                    .AddRedisInstrumentation()
                    .SetSampler(new RateSampler(0.25)) // Head sampling (application level), sample in the 1st service in the call chain, can use custom sampler like this or use default sampler of OTEL
                    //.AddConsoleExporter()
                    .AddOtlpExporter(options =>
                    {
                        options.Protocol = OtlpExportProtocol.Grpc;
                        options.Endpoint = otlpEndpoint;
                    }) // Jaeger support receive tracing data directly via OTLP
            )
            .WithMetrics(providerBuilder => providerBuilder
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddMeter("Microsoft.AspNetCore.Hosting")
                .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
                .AddMeter(ApplicationDiagnostics.Meter.Name)
                //.AddConsoleExporter()
                //.AddPrometheusExporter() // Prometheus use pull model to scrape metrics, use this exporter to create an endpoint for Prometheus to scrape data
                .AddOtlpExporter(options => { options.Endpoint = otlpEndpoint; })
            )
            .WithLogging(
                logging => logging
                    //.AddConsoleExporter()
                    .AddOtlpExporter(options => { options.Endpoint = otlpEndpoint; })
            );

        return builder;
    }
}