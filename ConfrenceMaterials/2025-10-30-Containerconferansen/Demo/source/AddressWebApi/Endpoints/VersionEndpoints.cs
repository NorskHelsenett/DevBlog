namespace AddressWebApi;

public static class VersionEndpoints
{
    public static void MapVersionEndpoints(this WebApplication app)
    {
        var versionInfoCommit = File.Exists("/app/git-commit.txt")
            ? File.ReadAllText("/app/git-commit.txt")
            : "Git commit put here if built in pipeline";
        var versionInfoBuild = File.Exists("/app/build-id.txt")
            ? File.ReadAllText("/app/build-id.txt")
            : "Pipeline ID put here if built in pipeline";
        var versionInfoPayload = $"<h1>Version Information</h1><h2>Commit ID</h2><p>{versionInfoCommit}</p><h2>Build ID (pipeline ID)</h2><p>{versionInfoBuild}</p>";
        app.MapGet("/version", (HttpContext httpContext, CancellationToken ct) =>
                Results.Text(content: versionInfoPayload,
                    contentType: "text/html",
                    statusCode: 200))
            .AllowAnonymous();
    }
}
