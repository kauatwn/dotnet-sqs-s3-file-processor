using DistributedFileProcessor.Application.Extensions;
using DistributedFileProcessor.Infrastructure.Extensions;
using DistributedFileProcessor.Worker;
using Serilog;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((_, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(builder.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", builder.Environment.ApplicationName)
        .WriteTo.Console();
});

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

builder.Services.AddHostedService<DocumentProcessingWorker>();

IHost host = builder.Build();

await host.RunAsync();