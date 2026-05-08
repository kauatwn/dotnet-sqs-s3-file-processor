using System.Globalization;
using System.Text;

Console.WriteLine("🚀 Starting CSV Generation...");

Directory.CreateDirectory("output");
string path = Path.Combine("output", "payload-100k.csv");

const int recordCount = 100_000;
Random random = Random.Shared;

using StreamWriter writer = new(path, append: false, Encoding.UTF8, bufferSize: 128 * 1024);

writer.WriteLine("Date,Amount,Description,AccountId");

for (int i = 1; i <= recordCount; i++)
{
    DateTime date = DateTime.Now.AddDays(-random.Next(0, 365));
    double amount = random.NextDouble() * 5000;

    writer.WriteLine(string.Create(CultureInfo.InvariantCulture,
        $"{date:yyyy-MM-dd},{amount:0.00},Simulated_Transaction_{i},ACC-{random.Next(1000, 99999)}"));
}

Console.WriteLine($"✅ Success! File '{path}' generated with {recordCount:N0} records.");