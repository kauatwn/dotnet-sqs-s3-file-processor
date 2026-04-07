using System.Globalization;
using System.Text;

Console.WriteLine("🚀 Starting CSV Generation...");

string filePath = Path.Combine("..", "transactions_1M.csv");
int recordCount = 1_000_000;

using (StreamWriter writer = new(filePath, append: false, Encoding.UTF8, bufferSize: 65536))
{
    writer.WriteLine("Date,Amount,Description,AccountId");

    Random rnd = new();
    DateTime baseDate = new(2023, 1, 1);

    for (int i = 1; i <= recordCount; i++)
    {
        string date = baseDate.AddDays(rnd.Next(0, 365)).ToString("yyyy-MM-dd");
        string amount = (rnd.NextDouble() * 5000).ToString("0.00", CultureInfo.InvariantCulture);
        string description = $"Simulated_Transaction_{i}";
        string accountId = $"ACC-{rnd.Next(1000, 99999)}";

        writer.WriteLine($"{date},{amount},{description},{accountId}");
    }
}

Console.WriteLine($"✅ Success! File '{filePath}' generated with {recordCount:N0} records.");