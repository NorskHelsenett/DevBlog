using System.Net;
using System.Text;

namespace AddressWebApi;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/healthz", () => Results.Ok("Started successfully")).AllowAnonymous();
        app.MapGet("/healthz/live", () => Results.Ok("Alive and well")).AllowAnonymous();
        app.MapGet("/healthz/ready", (IAddressStorage addressStorage) =>
        {
            if(addressStorage.Ready())
            {
                return Results.Ok("ready");
            }
            else
            {
                var offsetTarget = addressStorage.GetStartupTimeHightestTopicPartitionOffsets();
                var offsetCurrent = addressStorage.GetLastConsumedTopicPartitionOffsets();
                var sb = new StringBuilder();
                sb.Append('[').Append('\n');
                foreach(var target in offsetTarget)
                {
                    var current = offsetCurrent.FirstOrDefault(c => c.Topic == target.Topic && c.Partition == target.Partition);
                    sb.Append('\t').Append('{');
                    sb.Append($"\"Topic\": \"{target.Topic}\"").Append(",\t");
                    sb.Append($"\"Partition\": \"{target.Partition.Value}\"").Append(",\t");
                    sb.Append($"\"Current offset\": \"{current?.Offset.Value}\"").Append(",\t");
                    sb.Append($"\"Target offset at startup\": \"{target.Offset.Value}\"");
                    sb.Append('}').Append(',').Append('\n');
                }
                sb.Remove(sb.Length - 2, 1); // Remove trailing comma
                sb.Append(']');
                var statusString = sb.ToString();
                // Because kubernetes by default treats responses with status codes 200-399 as passes and 400+ as failures, blindly follow that convention and rely on the juicy status code.
                // https://kubernetes.io/docs/tasks/configure-pod-container/configure-liveness-readiness-startup-probes/#define-a-liveness-http-request
                return Results.Text(
                    content: $"Not ready. State hasn't caught up\n\nStatus:\n{statusString}",
                    contentType: "text/html",
                    contentEncoding: Encoding.UTF8,
                    statusCode: (int?) HttpStatusCode.ServiceUnavailable);
            }
        }).AllowAnonymous();
    }
}
