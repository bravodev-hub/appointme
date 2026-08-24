using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace AppointMe.Api.OpenTelemetry;

internal static class OpenTelemetryExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAppointMeOpenTelemetry()
        {
            services
                .AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService(serviceName: "AppointMe.Api", serviceVersion: "1.0.0"))
                .WithTracing(tracing =>
                {
                    tracing
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddSqlClientInstrumentation(options => { options.RecordException = true; })
                        .AddSource("Wolverine")
                        .AddOtlpExporter();
                })
                .WithMetrics(metrics =>
                {
                    metrics
                        .AddAspNetCoreInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddMeter("Wolverine*")
                        .AddOtlpExporter();
                });

            return services;
        }
    }
}
