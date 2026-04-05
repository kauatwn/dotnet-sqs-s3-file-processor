using System.Text.Json.Serialization;
using DistributedFileProcessor.API.Extensions;
using DistributedFileProcessor.Application.Extensions;
using DistributedFileProcessor.Infrastructure.Extensions;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog((services, loggerConfiguration) => 
{
    loggerConfiguration
        .ReadFrom.Configuration(builder.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", builder.Environment.ApplicationName)
        .WriteTo.Console();

    string? seqServerUrl = builder.Configuration["Seq:ServerUrl"];
    if (!string.IsNullOrWhiteSpace(seqServerUrl))
    {
        loggerConfiguration.WriteTo.Seq(seqServerUrl);
    }
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddOpenApi();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

var app = builder.Build();

await app.EnsureLocalStackResourcesAsync();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    await app.ApplyMigrationsAsync();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

await app.RunAsync();
