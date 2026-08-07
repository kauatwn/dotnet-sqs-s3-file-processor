using System.Text.Json;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using DistributedFileProcessor.Worker;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.LocalStack;
using Testcontainers.PostgreSql;

namespace DistributedFileProcessor.IntegrationTests.Abstractions;

public class IntegrationTestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("fileprocessor_db_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public LocalStackContainer LocalStack { get; } = new LocalStackBuilder("localstack/localstack:2026.07.0")
        .WithEnvironment("LOCALSTACK_AUTH_TOKEN", Environment.GetEnvironmentVariable("LOCALSTACK_AUTH_TOKEN"))
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Environment.SetEnvironmentVariable("AWS_ACCESS_KEY_ID", "test");
        Environment.SetEnvironmentVariable("AWS_SECRET_ACCESS_KEY", "test");
        Environment.SetEnvironmentVariable("AWS_REGION", "us-east-1");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            string localStackUrl = LocalStack.GetConnectionString();

            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _dbContainer.GetConnectionString(),
                ["AWS:AccessKey"] = "test",
                ["AWS:SecretKey"] = "test",
                ["AWS:ServiceURL"] = localStackUrl,
                ["AWS:SQS:ServiceURL"] = localStackUrl,
                ["AWS:S3:ServiceURL"] = localStackUrl,
                ["AWS:S3:ForcePathStyle"] = "true",
                ["AWS:Region"] = "us-east-1",

                ["AWS:SQS:QueueUrl"] = $"{localStackUrl}/000000000000/integration-test-queue",
                ["AWS:SQS:QueueName"] = "integration-test-queue",
                ["AWS:SQS:DlqName"] = "integration-test-dlq",

                ["AWS:S3:BucketName"] = "integration-test-bucket"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddHostedService<DocumentProcessingWorker>();

            string localStackUrl = LocalStack.GetConnectionString();
            BasicAWSCredentials credentials = new("test", "test");

            services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(credentials, new AmazonS3Config
            {
                ServiceURL = localStackUrl,
                AuthenticationRegion = "us-east-1",
                ForcePathStyle = true,
                UseHttp = true
            }));

            services.AddSingleton<IAmazonSQS>(_ => new AmazonSQSClient(credentials, new AmazonSQSConfig
            {
                ServiceURL = localStackUrl,
                AuthenticationRegion = "us-east-1",
                UseHttp = true
            }));
        });
    }

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_dbContainer.StartAsync(), LocalStack.StartAsync());
        await SetupLocalStackResourcesAsync();
    }

    private async Task SetupLocalStackResourcesAsync()
    {
        string localStackUrl = LocalStack.GetConnectionString();
        BasicAWSCredentials credentials = new("test", "test");

        using AmazonS3Client s3Client = new(credentials, new AmazonS3Config
        {
            ServiceURL = localStackUrl,
            AuthenticationRegion = "us-east-1",
            ForcePathStyle = true,
            UseHttp = true
        });

        using AmazonSQSClient sqsClient = new(credentials, new AmazonSQSConfig
        {
            ServiceURL = localStackUrl,
            AuthenticationRegion = "us-east-1",
            UseHttp = true
        });

        const string bucketName = "integration-test-bucket";
        const string mainQueueName = "integration-test-queue";
        const string dlqName = "integration-test-dlq";

        await s3Client.PutBucketAsync(bucketName);

        CreateQueueResponse dlqResponse = await sqsClient.CreateQueueAsync(dlqName);
        GetQueueAttributesResponse dlqAttr = await sqsClient.GetQueueAttributesAsync(dlqResponse.QueueUrl, ["QueueArn"]);
        string dlqArn = dlqAttr.QueueARN;

        string redrivePolicy = JsonSerializer.Serialize(new
        {
            deadLetterTargetArn = dlqArn,
            maxReceiveCount = "3"
        });

        CreateQueueResponse mainQueueResponse = await sqsClient.CreateQueueAsync(new CreateQueueRequest
        {
            QueueName = mainQueueName,
            Attributes = new Dictionary<string, string>
            {
                { "RedrivePolicy", redrivePolicy },
                { "VisibilityTimeout", "60" }
            }
        });

        GetQueueAttributesResponse mainAttr = await sqsClient.GetQueueAttributesAsync(mainQueueResponse.QueueUrl, ["QueueArn"]);
        string mainQueueArn = mainAttr.QueueARN;

        await s3Client.PutBucketNotificationAsync(new PutBucketNotificationRequest
        {
            BucketName = bucketName,
            QueueConfigurations =
            [
                new QueueConfiguration
                {
                    Events = [EventType.ObjectCreatedAll],
                    Queue = mainQueueArn
                }
            ]
        });
    }
}