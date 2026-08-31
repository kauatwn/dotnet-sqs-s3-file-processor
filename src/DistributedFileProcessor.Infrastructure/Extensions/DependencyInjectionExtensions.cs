using Amazon.S3;
using Amazon.SQS;
using DistributedFileProcessor.Application.Interfaces;
using DistributedFileProcessor.Domain.Interfaces;
using DistributedFileProcessor.Infrastructure.Configuration;
using DistributedFileProcessor.Infrastructure.Messaging;
using DistributedFileProcessor.Infrastructure.Parsing;
using DistributedFileProcessor.Infrastructure.Persistence;
using DistributedFileProcessor.Infrastructure.Persistence.Repositories;
using DistributedFileProcessor.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Retry;
using Polly.Timeout;
using System.Diagnostics.CodeAnalysis;

namespace DistributedFileProcessor.Infrastructure.Extensions;

[ExcludeFromCodeCoverage(Justification = "Pure dependency injection configuration")]
public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        AddOptions(services, configuration);
        AddAwsServices(services, configuration);
        AddPersistenceServices(services, configuration);
        AddResiliencePolicies(services);

        services.AddTransient<ITransactionFileParser, CsvTransactionFileParser>();

        return services;
    }

    private static void AddOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SqsOptions>()
            .Bind(configuration.GetSection(SqsOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<S3Options>()
            .Bind(configuration.GetSection(S3Options.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }

    private static void AddAwsServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDefaultAWSOptions(configuration.GetAWSOptions());
        services.AddAWSService<IAmazonSQS>();
        services.AddAWSService<IAmazonS3>();

        services.AddSingleton<IFileStorageService, S3FileStorageService>();
        services.AddSingleton<IMessageConsumer, SqsMessageConsumer>();
    }

    private static void AddPersistenceServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<FileProcessorDbContext>(options =>
        {
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"), npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null);
            })
            .UseSnakeCaseNamingConvention();
        });

        services.AddScoped<IDocumentProcessJobRepository, DocumentProcessJobRepository>();
        services.AddScoped<ITransactionRecordRepository, TransactionRecordRepository>();
    }

    private static void AddResiliencePolicies(IServiceCollection services)
    {
        services.AddResiliencePipeline(PipelineKeys.S3, builder =>
        {
            builder.AddTimeout(TimeSpan.FromSeconds(30));
            builder.AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<AmazonS3Exception>(),
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true
            });

            builder.AddTimeout(TimeSpan.FromSeconds(10));
        });

        services.AddResiliencePipeline(PipelineKeys.Sqs, builder =>
        {
            builder.AddTimeout(TimeSpan.FromSeconds(30));
            builder.AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<AmazonSQSException>()
                    .Handle<TimeoutRejectedException>(),
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromMilliseconds(500),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true
            });

            builder.AddTimeout(TimeSpan.FromSeconds(25));
        });
    }
}