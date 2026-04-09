using System.Globalization;
using System.Text;

Console.WriteLine("🚀 Starting CSV Generation...");

string path = Path.Combine("..", "transactions_1M.csv");
const int recordCount = 1_000_000;

using (StreamWriter writer = new(path, append: false, Encoding.UTF8, bufferSize: 128 * 1024))
{
    writer.WriteLine("Date,Amount,Description,AccountId");

    for (int i = 1; i <= recordCount; i++)
    {
        DateTime date = DateTime.Now.AddDays(Random.Shared.Next(0, 365));
        double amount = Random.Shared.NextDouble() * 5000;

        writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{date:yyyy-MM-dd},{amount:0.00},Simulated_Transaction_{i},ACC-{Random.Shared.Next(1000, 99999)}"));
    }
}

Console.WriteLine($"✅ Success! File '{path}' generated with {recordCount:N0} records.");