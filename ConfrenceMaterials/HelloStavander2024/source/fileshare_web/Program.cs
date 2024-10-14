using fileshare_web.Components;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped<IChungingProducer, MockProducer>();
builder.Services.AddScoped<FileController>();
// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCors();
HttpClient httpClient;
if (Environment.GetEnvironmentVariable("HTTPCLIENT_VALIDATE_EXTERNAL_CERTIFICATES") == "false")
{
    var handler = new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    };
    httpClient = new HttpClient(handler);
}
else
{
    httpClient = new HttpClient();
}

builder.Services.AddSingleton(httpClient);
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
    {
        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;

        var backendIdpUrl =
            Environment.GetEnvironmentVariable(
                "OIDC_IDP_ADDRESS_FOR_SERVER"); // "http://keycloak:8088/realms/lokalmaskin"
        var clientIdpUrl =
            Environment.GetEnvironmentVariable(
                "OIDC_IDP_ADDRESS_FOR_USERS"); // "http://localhost:8088/realms/lokalmaskin"

        options.Configuration = new()
        {
            Issuer = backendIdpUrl,
            AuthorizationEndpoint = $"{clientIdpUrl}/protocol/openid-connect/auth",
            TokenEndpoint = $"{backendIdpUrl}/protocol/openid-connect/token",
            JwksUri = $"{backendIdpUrl}/protocol/openid-connect/certs",
            JsonWebKeySet = FetchJwks($"{backendIdpUrl}/protocol/openid-connect/certs"),
            EndSessionEndpoint = $"{clientIdpUrl}/protocol/openid-connect/logout",
        };
        Console.WriteLine("Jwks: " + options.Configuration.JsonWebKeySet);
        foreach (var key in options.Configuration.JsonWebKeySet.GetSigningKeys())
        {
            options.Configuration.SigningKeys.Add(key);
            Console.WriteLine("Added SigningKey: " + key.KeyId);
        }

        options.ClientId = Environment.GetEnvironmentVariable("OIDC_CLIENT_ID"); // "my_app"

        options.TokenValidationParameters.ValidIssuers = [clientIdpUrl, backendIdpUrl];
        options.TokenValidationParameters.NameClaimType = "name"; // This is what populates @context.User.Identity?.Name
        options.TokenValidationParameters.RoleClaimType = "role";
        options.RequireHttpsMetadata =
            Environment.GetEnvironmentVariable("OIDC_REQUIRE_HTTPS_METADATA") != "false"; // disable only in dev env
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.SaveTokens = true;
        options.MapInboundClaims = true;

        // options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme);

JsonWebKeySet FetchJwks(string url)
{
    var result = httpClient.GetAsync(url).Result;
    if (!result.IsSuccessStatusCode || result.Content is null)
    {
        throw new Exception(
            $"Getting token issuers (Keycloaks) JWKS from {url} failed. Status code {result.StatusCode}");
    }

    var jwks = result.Content.ReadAsStringAsync().Result;
    return new JsonWebKeySet(jwks);
}

builder.Services.AddAuthorization();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorization();

builder.Services.AddHttpContextAccessor();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapPost("/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    await context.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme);
});

app.Run();
