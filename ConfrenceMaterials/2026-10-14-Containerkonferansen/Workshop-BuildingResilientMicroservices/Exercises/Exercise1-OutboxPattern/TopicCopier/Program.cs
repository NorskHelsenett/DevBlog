var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<KafkaConfigSource>();
builder.Services.AddSingleton<KafkaConfigDestination>();
builder.Services.AddHostedService<TopicCopier>();
var app = builder.Build();

app.Run();
