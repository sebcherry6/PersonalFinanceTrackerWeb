using System.Text.Json;
using PersonalFinanceTracker.Models;

namespace PersonalFinanceTracker.Services;

public static class DataService
{
    private static readonly string filePath = "transactions.json";

    public static void Save(List<Transaction> transactions)
    {
        string json = JsonSerializer.Serialize(transactions, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        File.WriteAllText(filePath, json);
    }

    public static List<Transaction> Load()
    {
        if (!File.Exists(filePath))
            return new List<Transaction>();

        string json = File.ReadAllText(filePath);

        return JsonSerializer.Deserialize<List<Transaction>>(json) ?? new List<Transaction>();
    }
}