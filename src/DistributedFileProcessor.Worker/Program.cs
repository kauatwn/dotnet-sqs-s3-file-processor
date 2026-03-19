using DistributedFileProcessor.Application.Extensions;
using DistributedFileProcessor.Infrastructure.Extensions;
using DistributedFileProcessor.Worker;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", builder.Environment.ApplicationName)
    .WriteTo.Console()
    .WriteTo.Seq(builder.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341"));

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

builder.Services.AddHostedService<DocumentProcessingWorker>();

var host = builder.Build();

await host.EnsureLocalStackResourcesAsync();

await host.RunAsync();
