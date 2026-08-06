using System.Text.Json.Serialization;
using DistributedFileProcessor.Application.Extensions;
using DistributedFileProcessor.Infrastructure.Extensions;
using Serilog;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog((_, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(builder.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", builder.Environment.ApplicationName)
        .WriteTo.Console();
});

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    await app.ApplyMigrationsAsync();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

await app.RunAsync();