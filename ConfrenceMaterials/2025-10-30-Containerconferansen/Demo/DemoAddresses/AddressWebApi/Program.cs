global using static AddressWebApi.ConfigKeys;
using System.Text.Json.Serialization;
using AddressWebApi;

// This is a web app, because it will be long running and therefore should have health check endpoints
var builder = WebApplication.CreateBuilder(args);

// To deserialize the json enums
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
{
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.SetupOpenTelemetry();

builder.Services.AddSingleton<IAddressStorage, AddressStorage>();
// builder.Services.AddSingleton<IAddressStorage, AddressStorageDict>();
// builder.Services.AddSingleton<RefinedAddressStreamProducer>();
builder.Services.AddHostedService<RefinedAddressStreamConsumer>();
// builder.Services.AddHostedService<RawAddressStreamConsumer>();

var app = builder.Build();

// As this is at the moment is a convenience re-host of a public dataset,
// which can be hosted/run in many instances in a throwaway fashion wherever you might need it,
// and the emergency plan if anything is wrong with it in any way is shutting it down and dealing with its absence,
// don't gate the swagger ui behind any kind of dev env or auth.
app.UseSwagger();
app.UseSwaggerUI();

app.MapHealthEndpoints();
app.MapVersionEndpoints();
app.MapQueryEndpoints();

app.Run();
