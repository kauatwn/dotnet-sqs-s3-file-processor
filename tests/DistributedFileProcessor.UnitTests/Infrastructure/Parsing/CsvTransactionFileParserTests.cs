using System.Text;
using CsvHelper;
using CsvHelper.TypeConversion;
using DistributedFileProcessor.Domain.Entities;
using DistributedFileProcessor.Infrastructure.Parsing;

namespace DistributedFileProcessor.UnitTests.Infrastructure.Parsing;

[Trait("Category", "Unit")]
public class CsvTransactionFileParserTests
{
    private readonly CsvTransactionFileParser _sut = new();

    private static MemoryStream CreateStreamFromCsv(string csvContent)
    {
        byte[] byteArray = Encoding.UTF8.GetBytes(csvContent);
        return new MemoryStream(byteArray);
    }

    [Fact(DisplayName = "Should successfully parse a valid CSV stream")]
    public async Task ParseStreamAsync_ShouldReturnTransactionRecords_WhenCsvIsValid()
    {
        // Arrange
        Guid expectedJobId = Guid.NewGuid();
        const string validCsv = """
                                Date,Amount,Description,AccountId
                                2023-01-01,150.75,Supermarket,ACC-123
                                2023-01-02,50.00,Gas Station,ACC-456
                                """;

        MemoryStream stream = CreateStreamFromCsv(validCsv);
        List<TransactionRecord> result = [];

        // Act
        await foreach (TransactionRecord record in _sut.ParseStreamAsync(stream, expectedJobId, TestContext.Current.CancellationToken))
        {
            result.Add(record);
        }

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count);

        Assert.Equal(expectedJobId, result[0].JobId);
        Assert.Equal(new DateTime(2023, 1, 1), result[0].TransactionDate);
        Assert.Equal(150.75m, result[0].Amount);
        Assert.Equal("Supermarket", result[0].Description);
        Assert.Equal("ACC-123", result[0].AccountId);
    }

    [Fact(DisplayName = "Should throw MissingFieldException when header is missing or wrong")]
    public async Task ParseStreamAsync_ShouldThrowException_WhenHeaderIsInvalid()
    {
        // Arrange
        Guid expectedJobId = Guid.NewGuid();
        const string invalidCsv = """
                                  WrongColumn1,WrongColumn2,Description,AccountId
                                  2023-01-01,150.75,Supermarket,ACC-123
                                  """;

        MemoryStream stream = CreateStreamFromCsv(invalidCsv);

        // Act
        async Task Act()
        {
            await foreach (TransactionRecord _ in _sut.ParseStreamAsync(stream, expectedJobId, TestContext.Current.CancellationToken))
            {
                // Apenas itera para forçar o erro
            }
        }

        // Assert
        var exception = await Assert.ThrowsAsync<HeaderValidationException>(Act);
        Assert.NotNull(exception);
    }

    [Fact(DisplayName = "Should throw TypeConverterException when data format is invalid")]
    public async Task ParseStreamAsync_ShouldThrowException_WhenDataFormatIsInvalid()
    {
        // Arrange
        Guid expectedJobId = Guid.NewGuid();
        const string invalidDataCsv = """
                                Date,Amount,Description,AccountId
                                2023-01-01,NotANumber,Supermarket,ACC-123
                                """;
        
        MemoryStream stream = CreateStreamFromCsv(invalidDataCsv);

        // Act
        async Task Act()
        {
            await foreach (TransactionRecord _ in _sut.ParseStreamAsync(stream, expectedJobId, TestContext.Current.CancellationToken))
            {
                // Apenas itera para forçar o erro
            }
        }

        // Assert
        var exception = await Assert.ThrowsAsync<TypeConverterException>(Act);
        Assert.NotNull(exception);
    }
}