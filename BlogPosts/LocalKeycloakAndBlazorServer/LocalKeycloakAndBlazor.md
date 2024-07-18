Local development with Keycloak and Blazor Server
===

Published:
ToDo - link to medium

See complete example at https://github.com/NorskHelsenett/DevBlog/tree/main/BlogPosts/LocalKeycloakAndBlazorServer/SampleProject

# Motivation

[OpenID Connect](https://en.wikipedia.org/wiki/OpenID#OpenID_Connect_(OIDC)) (OIDC) and [OAuth](https://en.wikipedia.org/wiki/OAuth) auth is fun, but abusing you hosted identity provider (IDP) which others rely on working and needs to be operated securely is not fun, both when testing out new authorization properties on users, or when you just want to work offline.

[Blazor](https://dotnet.microsoft.com/en-us/apps/aspnet/web-apps/blazor) is also fun! But getting it to work smoothly with a local Keycloak instance can be less fun. Especially if you want to do something interesting like using the tokens to call a second service that uses the same Keycloak instance on behalf of the user.

In this post we'll explore how to get Keycloak up and running locally, so that you can have a service handling login and issuing tokens to your users. Then we set up a Blazor Server app that uses the Keycloak instance to log in users, and act on behalf of the users against an semi-external third party endpoint.

Table of Contents

- [Motivation](#motivation)
- [Context](#context)
- [Setting up Keycloak](#setting-up-keycloak)
- [Setting up a new Blazor app that uses Keycloak](#setting-up-a-new-blazor-app-that-uses-keycloak)

# Context

> A beginning is the time for taking the most delicate care that the balances are correct.

This was written in july 2024, by Simon Randby while working for [Norsk Helsenett](https://www.nhn.no/). The used version of [Keycloak](https://www.keycloak.org/) was 25.0.1, and [dotnet](https://dotnet.microsoft.com) was 8.0.303. The [Docker](https://docs.docker.com/) version was 27.0.3. All Docker images run were for the arm architecture.

# Setting up Keycloak

Before you can get properly started, you have to create a Keycloak [realm](https://www.keycloak.org/docs/latest/server_admin/#core-concepts-and-terms) for your application. This is where you'll register the app/client so that it can send users to the Keycloak instance to log in, and where you can set up users for the relevant application.

In theory you might get away with running everything in the "master" realm, but if you actually run Keycloak in production you will most likely be using a separate realm properly. Hence the first time set up.

## Start docker compose service

Before you start the docker compose service, ensure that you have created a folder named "keycloak/" besides the compose file, so that the config work can be exported later.

To start out, spin up your local Keycloak instance like this:

```yaml
services:
  keycloak:
    image: quay.io/keycloak/keycloak
    container_name: keycloak
    user: 1000:1001
    volumes:
      - "./keycloak:/opt/keycloak/data/import"
    ports:
      - 8088:8088
    environment:
      KEYCLOAK_ADMIN: admin
      KEYCLOAK_ADMIN_PASSWORD: "password"
      KC_PROXY: "none"
      KC_LOG_LEVEL: "INFO"
      KC_HTTP_PORT: "8088"
      KC_HTTP_HOST: 0.0.0.0
    entrypoint:
      - '/bin/bash'
      - '-c'
      - |
        /opt/keycloak/bin/kc.sh start-dev
```

## Log in

Now you can visit it at http://localhost:8088, and log in with the tried and true "Username: admin; Password: password" combination.

<figure>
  <img src="./images/Keycloak Setup - 01 - Log in to KC.png" alt="Landing page after login"/>
  <figcaption>Landing page when you've logged in.</figcaption>
</figure>

## Create realm

Once you have logged in, click the realm list in the top left corner to create your local realm.

<figure>
  <img src="./images/Keycloak Setup - 02 - List realms.png" alt="Realm drop down list"/>
  <figcaption>Realm drop down list</figcaption>
</figure>

### Fill in realm details

Click the "Create realm" button, and fill in a suiting name for your new realm. Beware that this name will show up in URLs and various configs, so short and sweet and alphanumeric ascii brings joy.

In the example here, I've called it "lokalmaskin".

<figure>
  <img src="./images/Keycloak Setup - 03 - Create realm step 1.png" alt="Realm creation form"/>
  <figcaption>Realm creation form</figcaption>
</figure>

Once you've clicked the "Create" button, you'll be taken to the overview page for your new realm.

<figure>
  <img src="./images/Keycloak Setup - 04 - Realm created.png" alt="Realm overview page"/>
  <figcaption>Realm overview page</figcaption>
</figure>

## Configure realm

Now that your realm has been created, you can navigate to "Realm settings" in the left hand side menu. There you will want to set the "Require SSL" option to "None". (After all you are not practicing properly configuring a service that you will operate over time, this tutorial is about getting a good enough local development up and running so that you can practice hacking on your actual product with realistic enough dependencies.)

<figure>
  <img src="./images/Keycloak Setup - 05 - Realm settings - disable https required.png" alt="Realm settings page"/>
  <figcaption>Realm configuration page</figcaption>
</figure>

Once you've updated it, remember to click on the "Save" button.

## Create client

Now that your realm is set up, it's time to create a client/an application in Keycloak. This is so that you in production can set up the security properly and only allow clients you trust to send users to the Keycloak instance for authentication. However, in this introductory local setup, we'll eschew the fine details and just take the shortest path to an instance throwing tokens at your browser that you can give to your app running locally.

To begin, go th the "Clients" Page in the left hand menu, and click the "Create client" button.

<figure>
  <img src="./images/Keycloak Setup - 06 - Create client step 1.png" alt="Realm clients overview page"/>
  <figcaption>Overview page for clients registered to the realm</figcaption>
</figure>

### General settings

On the general settings page, you should only need to fill in the "Client ID" field. Once again, this will appear in various configs, so keep the name as simple as can while it still makes some sense for you use case. In this example I've called it "my_app". Leave the "Client type" field with it's default "OpenID Connect" value.

<figure>
  <img src="./images/Keycloak Setup - 07 - Create client step 2.png" alt="Create client general settings"/>
  <figcaption>Create client - general settings</figcaption>
</figure>

### Capability config

Once you've forged a name you're happy with, click next and go on to the "Capability config" form. Here, you can leave everything in it's default state (Client authentication: Off, Authorization: Off, Authentication flow: { Standard flow: true, Direct access grants: true, Implicit flow: false, Service account roles: false, OAuth 2.0 Device Authorization Grant: false, OIDC CIBA Grant: false}).

<figure>
  <img src="./images/Keycloak Setup - 08 - Create client step 3.png" alt="Create client capability config"/>
  <figcaption>Create client - capability config</figcaption>
</figure>

### Login settings

Here you will set up the addresses your application can send users to log in from, where the uses are allowed to be sent back to (it would be somewhat boring if after logging in the users took the shiny new token for you and just went straight to another webpage and gave it to them instead), and where they can be sent after they log out. Because the primary objective here is that getting ahold of OIDC tokens form a trusted provider just works, we'll allow anything by setting "Root URL" empty, "Home URL" also empty, "Valid redirect URIs" to "\*", "Valid post logout redirect URIs" to "+", and "Web origins" to "\*",

<figure>
  <img src="./images/Keycloak Setup - 09 - Create client step 4.png" alt="Create client login settings form"/>
  <figcaption>Create client - setup shipping addresses</figcaption>
</figure>

Once you're done and click save, you should be taken to the client/app overview.

<figure>
  <img src="./images/Keycloak Setup - 10 - Creae client step 5 after create view.png" alt="Create client client overview page"/>
  <figcaption>Create client - overview page after save</figcaption>
</figure>

At this point there is nothing more to do here, so the next step is creating a user in the realm, so that you can log in!

## Create user

To create your user, click "Users" in left hand menu.

<figure>
  <img src="./images/Keycloak Setup - 11 - Create user step 0 users overview.png" alt="All users overview page"/>
  <figcaption>All users overview page</figcaption>
</figure>

Click on the git "Create new user" button in the middle of the screen.

### Fill in form

Once you've clicked the "Create new user" button, you'll be greeted by a form where you can fill in the details you want. The only thing that's really needed to create a user that can be issued tokens is the user name, so you don't actually have to fill out anything else. Only beware the "Required user actions" settings, which you want to leave empty. In real life, it's good to get users to verify their emails, change/set up their own password at first login, etc. On localhost, where we're not currently creating a login service but merely automating getting the bare minimum of validatable tokens, we don't want any of that hassle.

<figure>
  <img src="./images/Keycloak Setup - 12 - Create user step 1 fill in users details.png" alt="Create user form"/>
  <figcaption>Create user form</figcaption>
</figure>

After you've clicked the "Create" button you'll be taken to the page with the overview for your new user.

<figure>
  <img src="./images/Keycloak Setup - 13 - Create user step 2 land on user overview.png" alt="New user overview"/>
  <figcaption>New user overview</figcaption>
</figure>

### Set up password

Now, you'll want to set up the password for the user. Start with going to the "Credentials" tab for the user.

<figure>
  <img src="./images/Keycloak Setup - 14 - Create user step 3 go to users credentials.png" alt="User Credentials tab"/>
  <figcaption>User Credentials tab</figcaption>
</figure>

Click on the "Set password" button and fill out the form.

<figure>
  <img src="./images/Keycloak Setup - 15 - Create user step 4 set users password.png" alt="Set up password for user form"/>
  <figcaption>Set up password for user form</figcaption>
</figure>

Make sure you disable the "Temporary" toggle, as you don't want to be prompted to change the password at first login to localhost.

At this point, for some weird reason, you'll be asked if you really want to set the password for the user. Click the big red "Save password" button to move ahead.

<figure>
  <img src="./images/Keycloak Setup - 16 - Crete user step 5 click yes to scarey warning.png" alt="User password - confirm that you want to click the button you clicked in the previous popup"/>
  <figcaption>User password - confirm that you want to click the button you clicked in the previous popup</figcaption>
</figure>

Once you've cleared this hurdle, you'll be greeted by the credentials overview page showing the password you just created for the user.

<figure>
  <img src="./images/Keycloak Setup - 17 - Create user step 6 back to credentials overview.png" alt="User credentials overview after password creation"/>
  <figcaption>User credentials overview after password creation</figcaption>
</figure>

Finally, to save on future tech support calls of people wondering what the localhost user password was set to instead of just simply resetting it, update the label so it says what the password is.

<figure>
  <img src="./images/Keycloak Setup - 18 - Create user step 7 describe dev credential.png" alt="User credentials overview with updated description"/>
  <figcaption>User credentials overview with updated description</figcaption>
</figure>

## Export the settings

### Perform the export

At this point, you've configured all the first time, one off, settings in Keycloak. It is time to export the setup to a config file, so that you can put it somewhere safe and persistent so that humanity doesn't have to spend any more of our time doing this ever again.

Because you have already created the "keycloak/" directory next to your docker compose and have mapped it to the "/opt/keycloak/data/import/" directory inside the container, all we have to do now is exec into the container and run the export.

```sh
docker exec -it keycloak "/bin/bash"
/opt/keycloak/bin/kc.sh export --file /opt/keycloak/data/import/keycloak-lokalmaskin.json
```

### Set up import at startup

Now the only thing that remains is setting up the docker compose service so that it imports the realm settings you've painstakingly created before startup. To do this, run the `kc.sh import --file ...` before you start up Keycloak with `kc.sh start-dev`.

```yaml
services:
  keycloak:
    image: quay.io/keycloak/keycloak
    container_name: keycloak
    user: 1000:1001
    volumes:
      - "./keycloak:/opt/keycloak/data/import"
    ports:
      - 8088:8088
    environment:
      KEYCLOAK_ADMIN: admin
      KEYCLOAK_ADMIN_PASSWORD: "password"
      KC_PROXY: "none"
      KC_LOG_LEVEL: "INFO"
      KC_HTTP_PORT: "8088"
      KC_HTTP_HOST: 0.0.0.0
    entrypoint:
      - '/bin/bash'
      - '-c'
      - |
        echo 'To export config of instance you have set up, run: /opt/keycloak/bin/kc.sh export --file /opt/keycloak/data/import/keycloak-lokalmaskin.json'
        echo "Importing realms"
        /opt/keycloak/bin/kc.sh import --file /opt/keycloak/data/import/keycloak-lokalmaskin.json
        echo "Staring keycloak service"
        /opt/keycloak/bin/kc.sh start-dev
```

You are now ready to start using your Keycloak instance!

A sample of the exported config set up as described above can be found here [keycloak-lokalmaskin.json](./SampleProject/keycloak/keycloak-lokalmaskin.json).

# Setting up a new Blazor app that uses Keycloak

Now, just getting Keycloak up and running by itself is not really interesting or useful before we have an example of how to utilize it. Therefore, this is a section on how to get a [Blazor Server](https://learn.microsoft.com/en-us/aspnet/core/blazor/hosting-models?view=aspnetcore-8.0) application upp and running using the Keycloak instance for signing in users. Further, it will illustrate how you can access the access token to call a separate service using the same Keycloak instance for auth. This last part is generally poorly documented, because you're not really supposed to be doing it. In a perfect world, both you and the other service would rely on the "audience" field saying which app the token is intended for, and reject it if it is not specifically you. And so you would set up a separate process for authentication between the services. However, in the murky waters of subdivided organizations and microservices, acting on behalf of the user to automate usage of an internal API can do wonders for the accessability of that service even if you cannot otherwise alter it. So don't do this at home. Unless you know it's fine. Then it's fine.

Start out by making a new dotnet Blazor project, and add the [Microsoft.AspNetCore.Authentication.OpenIdConnect](https://www.nuget.org/packages/Microsoft.AspNetCore.Authentication.OpenIdConnect) nuget package.

```sh
dotnet new gitignore

dotnet new blazor --name blazorkc --output .

dotnet add package Microsoft.AspNetCore.Authentication.OpenIdConnect
```

Add a basic docker compose for running and configuring the application

```yaml
services:
  blazorkc:
    image: blazorkc
    container_name: blazorkc
    build:
      context: .
      dockerfile_inline: |
        FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
        WORKDIR /app

        FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
        WORKDIR /src
        COPY ["blazorkc.csproj", "./"]
        RUN dotnet restore "blazorkc.csproj"
        COPY . .
        WORKDIR "/src/"
        RUN dotnet build "blazorkc.csproj" -c Release -o /app/build

        FROM build AS publish
        RUN dotnet publish "blazorkc.csproj" -c Release -o /app/publish /p:UseAppHost=false

        FROM base AS final
        WORKDIR /app
        COPY --from=publish /app/publish .
        ENTRYPOINT ["dotnet", "blazorkc.dll"]
    ports:
      - "8080:80"
    environment:
      ASPNETCORE_URLS: "http://+:80"
      LOGGING__LOGLEVEL__DEFAULT: "Trace"
      LOGGING__LOGLEVEL__MICROSOFT: "Warning"

      OIDC_IDP_ADDRESS_FOR_SERVER: "http://keycloak:8088/realms/lokalmaskin"
      OIDC_IDP_ADDRESS_FOR_USERS: "http://localhost:8088/realms/lokalmaskin"
      OIDC_CLIENT_ID: "my_app"
      OIDC_REQUIRE_HTTPS_METADATA: "false"

      HTTPCLIENT_VALIDATE_EXTERNAL_CERTIFICATES: "false"
```

Extend Program.cs to enable the newly created app to talk OIDC with the Keycloak server by adding this:

```cs
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

...

HttpClient httpClient;
if (Environment.GetEnvironmentVariable("HTTPCLIENT_VALIDATE_EXTERNAL_CERTIFICATES") == "false")
{
    // Needed locally when Keycloak is not assigned any proper certificates
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

    var backendIdpUrl = Environment.GetEnvironmentVariable("OIDC_IDP_ADDRESS_FOR_SERVER"); // "http://keycloak:8088/realms/lokalmaskin"
    var clientIdpUrl = Environment.GetEnvironmentVariable("OIDC_IDP_ADDRESS_FOR_USERS"); // "http://localhost:8088/realms/lokalmaskin"

    options.Configuration = new ()
    {
        Issuer = backendIdpUrl,
        AuthorizationEndpoint = $"{clientIdpUrl}/protocol/openid-connect/auth",
        TokenEndpoint = $"{backendIdpUrl}/protocol/openid-connect/token",
        JwksUri = $"{backendIdpUrl}/protocol/openid-connect/certs",
        JsonWebKeySet = FetchJwks($"{backendIdpUrl}/protocol/openid-connect/certs"),
        EndSessionEndpoint = $"{clientIdpUrl}/protocol/openid-connect/logout",
    };
    Console.WriteLine("Jwks: "+options.Configuration.JsonWebKeySet);
    foreach(var key in options.Configuration.JsonWebKeySet.GetSigningKeys())
    {
        options.Configuration.SigningKeys.Add(key);
        Console.WriteLine("Added SigningKey: "+ key.KeyId);
    }

    options.ClientId = Environment.GetEnvironmentVariable("OIDC_CLIENT_ID"); // "my_app"

    options.TokenValidationParameters.ValidIssuers = [clientIdpUrl,backendIdpUrl];
    options.TokenValidationParameters.NameClaimType = "name"; // This is what populates @context.User.Identity?.Name
    options.TokenValidationParameters.RoleClaimType = "role";
    options.RequireHttpsMetadata = Environment.GetEnvironmentVariable("OIDC_REQUIRE_HTTPS_METADATA") != "false"; // disable only in dev env
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

...

app.MapPost("/logout", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    await context.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme);
});

...
```

Add to Components/_Imports.razor to ensure all interaction with app is after user has logged in

```razor
@using Microsoft.AspNetCore.Authorization
@attribute [Authorize]
```

Because what makes this fun is that we run all as Blazor Server app, to make it so, add this to Components/App.Razor

```razor
...
<head>
  ...
  <HeadOutlet @rendermode="InteractiveServer" />
</head>

<body>
  <Routes @rendermode="InteractiveServer" />
  ...
</body>
...
```

To enable log out, add this to Components/Layout/NavMenu.razor

```razor
@using Microsoft.AspNetCore.Components.Authorization

...

<div class="nav-item px-3">
    <AuthorizeView>
        <Authorized>
            <div class="nav-link">
                <span class="bi bi-person-fill" aria-hidden="true"></span> @context.User.Identity?.Name
                @* Logged in as @context.User.Claims.Aggregate("", (s, claim) => s += ";\n" + claim ) *@
            </div>
        </Authorized>
    </AuthorizeView>
</div>

...

<div class="nav-item px-3">
    <AuthorizeView>
        <Authorized>
            <form action="Logout" method="post">
                <AntiforgeryToken />
                <button type="submit" class="nav-link">
                    <span class="bi bi-box-arrow-left" aria-hidden="true"></span> Logout
                </button>
            </form>
        </Authorized>
    </AuthorizeView>
</div>

...
```

Note that this also adds a section showing the users name. You can omit it, but is nice to be able to see it at a glance.

To check that the login works/changes anything, and that our token is indeed accessible ready to be hurled at our sibling service, you can add this to an inconspicuous page, like Components/Pages/Home.razor

```razor
@using Microsoft.AspNetCore.Authentication
@using Microsoft.AspNetCore.Components.Authorization
@inject IHttpContextAccessor HttpContextAccessor

...

<h2>Parsed Claims</h2>
<AuthorizeView>
    <Authorized>
        <div class="text-break">
            @((MarkupString)@context.User.Claims.Aggregate("", (s, claim) => s += "<br />" + claim )["<br />".Length..])
        </div>
    </Authorized>
</AuthorizeView>

<h2>Raw Token</h2>
<p class="text-break">@HttpContextAccessor.HttpContext!.GetTokenAsync("access_token").Result</p>
```

The important "you should not actually be doing this" part is the HttpContextAccessor which we use to get at the access token.

## Using access token to call other service on behalf of user

Now on to illustrate that you can call an external service/API which uses the same Keycloak instance for tokens, to make requests on behalf of the user. This can be useful if you for instance have an external service that has information about a users resources beyond what's reasonable to put in an access token, but you want to retrieve and use to shape the flow in your application.

To do this, start by making a simple background service where we'll perform the request.

```cs
public class ExternalInvokerService(HttpClient httpClient)
{
    public async Task<string> PokeExternalService(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri("http://host.docker.internal:3001"));
        request.Headers.Add("Authorization", $"Bearer {accessToken}");
        try
        {
            var response = await httpClient.SendAsync(request);
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception e) { Console.WriteLine(e); }
        return "Failed";
    }
}
```

Beware that it's hardcoded to run within a Docker container and target a specific port on your host. If you run it outside Docker just go directly to localhost instead.

Now you can register the service you just created in Program.cs

```cs
...
builder.Services.AddScoped<ExternalInvokerService>();
...
```

And call it from somewhere in your front end like this, for instance from Components/Pages/Home.razor

```razor
...
@inject ExternalInvokerService ExternalInvokerService

...

<h2>Poke external service</h2>
<button class="btn btn-outline-primary" @onclick="Poke">Poke</button>
<p>@_pokeResult</p>

@code
{
    private string? _pokeResult = "Before request";
    private async void Poke()
    {
        _pokeResult = "Before request";
        var accessToken = HttpContextAccessor.HttpContext?.GetTokenAsync("access_token").Result ?? "";
        var result = await ExternalInvokerService.PokeExternalService(accessToken);
        _pokeResult = result;
    }
}
```

To test that it works, start a netcat service in a new terminal, so that you can see that the request comes through with the required access token

```sh
while true ; do echo -e "HTTP/1.0 200 OK\nContent-Length: 14\nContent-Type: text/plain\n\nHello from nc\n" | nc -l 3001 ; done
```

Blazor will not like the response (unlike for instance curl), but the only thing that matters at this point is seeing that the request is what we expect.

And at this point, everything should just work!
