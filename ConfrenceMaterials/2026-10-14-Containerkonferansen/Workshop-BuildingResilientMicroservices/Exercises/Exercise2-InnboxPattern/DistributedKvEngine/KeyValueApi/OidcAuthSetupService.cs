using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

public static class OidcAuthSetupService
{
    public static IServiceCollection AddOidcAuth(this IServiceCollection appBuilderServices)
    {
        appBuilderServices.AddAuthentication().AddJwtBearer("OurBearerScheme", options =>
        {
            var backendIdpUrl = Environment.GetEnvironmentVariable(OIDC_IDP_ADDRESS_FOR_SERVER); // "http://keycloak:8088/realms/lokalmaskin"
            var clientIdpUrl = Environment.GetEnvironmentVariable(OIDC_IDP_ADDRESS_FOR_USERS); // "http://localhost:8088/realms/lokalmaskin"
            var authorizationEndpoint = Environment.GetEnvironmentVariable(OIDC_AUTHORIZATION_ENDPOINT);
            if(string.IsNullOrEmpty(authorizationEndpoint))
            {
                authorizationEndpoint = $"{clientIdpUrl}/protocol/openid-connect/auth";
            }
            var tokenEndpoint = Environment.GetEnvironmentVariable(OIDC_TOKEN_ENDPOINT);
            if(string.IsNullOrEmpty(tokenEndpoint))
            {
                tokenEndpoint = $"{backendIdpUrl}/protocol/openid-connect/token";
            }
            var jwksUri = Environment.GetEnvironmentVariable(OIDC_JWKS_URI);
            if(string.IsNullOrEmpty(jwksUri))
            {
                jwksUri = $"{backendIdpUrl}/protocol/openid-connect/certs";
            }
            var endSessionEndpoint = Environment.GetEnvironmentVariable(OIDC_END_SESSION_ENDPOINT);
            if(string.IsNullOrEmpty(endSessionEndpoint))
            {
                endSessionEndpoint = $"{clientIdpUrl}/protocol/openid-connect/logout";
            }
            options.Configuration = new(){
                Issuer = backendIdpUrl,
                AuthorizationEndpoint = authorizationEndpoint,
                TokenEndpoint = tokenEndpoint,
                JwksUri = jwksUri,
                JsonWebKeySet = FetchJwks(GetHttpClient(), jwksUri),
                EndSessionEndpoint = endSessionEndpoint,
            };
            Console.WriteLine($"Public keys posted by trusted OpenIdentityConnect IdentityProvider at \"{jwksUri}\" (formatted as JsonWebKeySet): {System.Text.Json.JsonSerializer.Serialize(options.Configuration.JsonWebKeySet)}");
            foreach (var key in options.Configuration.JsonWebKeySet.GetSigningKeys())
            {
                options.Configuration.SigningKeys.Add(key);
                Console.WriteLine($"Added SigningKey with KeyID: {key.KeyId}");
            }

            options.TokenValidationParameters.ValidIssuers = [clientIdpUrl, backendIdpUrl];
            options.TokenValidationParameters.NameClaimType = "name"; // This is what populates @context.User.Identity?.Name
            options.TokenValidationParameters.RoleClaimType = "role";
            options.RequireHttpsMetadata =
                Environment.GetEnvironmentVariable(OIDC_REQUIRE_HTTPS_METADATA) != "false"; // disable only in dev env
            options.MapInboundClaims = true;
            options.Audience = Environment.GetEnvironmentVariable(OIDC_AUDIENCE);
        });
        // Require auth by default, have to use `.AllowAnonymous();` if you want e.g. health checks to be anonymous
        // Use `.AddAuthorization();` if you want the inverse, i.e. only endpoints where you've added `.RequireAuthorization();` to require user is logged in.
        appBuilderServices.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());
        return appBuilderServices;
    }

    private static JsonWebKeySet FetchJwks(HttpClient httpClient, string url)
    {
        var result = httpClient.GetAsync(url).Result;
        if (!result.IsSuccessStatusCode || result.Content is null)
        {
            throw new Exception($"Getting JWKS belonging to token issuer from {url} failed. Status code {result.StatusCode}");
        }

        var jwks = result.Content.ReadAsStringAsync().Result;
        return new JsonWebKeySet(jwks);
    }

    private static HttpClient GetHttpClient()
    {
        if (Environment.GetEnvironmentVariable(OIDC_HTTPCLIENT_VALIDATE_EXTERNAL_CERTIFICATES) == "false")
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };
            return new HttpClient(handler);
        }
        return new HttpClient();
    }
}
