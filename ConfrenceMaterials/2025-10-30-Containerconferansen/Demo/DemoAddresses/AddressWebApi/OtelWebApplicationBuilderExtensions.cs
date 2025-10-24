using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

public static class WebApplicationBuilderExtensions
{
    public static WebApplicationBuilder SetupOpenTelemetry(this WebApplicationBuilder builder)
    {
        var serviceName = builder.Configuration.GetValue<string>("SERVICE_NAME") ??
                          builder.Environment.ApplicationName;
        var environment = builder.Configuration.GetValue<string>("DEPLOYMENT_ENVIRONMENT") ??
                          builder.Environment.EnvironmentName;
        IEnumerable<KeyValuePair<string, object>> attributes = [ new("deployment.environment.name", environment) ];

        var tracesSourceName = serviceName;
        var metricsSourceName = serviceName;
        builder.Services.AddSingleton(new System.Diagnostics.ActivitySource(tracesSourceName));
        builder.Services.AddSingleton(new System.Diagnostics.Metrics.Meter(metricsSourceName));

        builder
            .Services
            .AddOpenTelemetry()
            .ConfigureResource(resource =>
            {
                resource
                    .AddService(serviceName)
                    .AddAttributes(attributes);
            })
            .WithTracing(traces =>
            {
                traces
                    // .AddSource("*", "AppNamespace.*")
                    .AddSource(tracesSourceName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter();
            })
            .WithLogging(logs =>
            {
                logs.AddOtlpExporter();
            })
            .WithMetrics(metrics =>
            {
                metrics
                    // .AddMeter("*", "AppNamespace.*")
                    .AddMeter(metricsSourceName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddOtlpExporter();
            });

        return builder;
    }
}
