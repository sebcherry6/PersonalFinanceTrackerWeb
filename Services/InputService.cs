using PersonalFinanceTracker.Models;

namespace PersonalFinanceTracker.Services;

public static class InputService
{
    public static decimal ReadDecimal(string message)
    {
        decimal value;

        while (true)
        {
            Console.Write(message);

            string input = Console.ReadLine();

            if (decimal.TryParse(input, out value))
                return value;

            Console.WriteLine("Invalid number. Try again.");
        }
    }

    public static string ReadString(string message)
    {
        Console.Write(message);

        string input = Console.ReadLine();

        return string.IsNullOrWhiteSpace(input) ? "Unknown" : input;
    }

    public static Category ReadCategory()
    {
        Console.WriteLine("Choose category:");

        foreach (var c in Enum.GetValues(typeof(Category)))
            Console.WriteLine($"- {c}");

        while (true)
        {
            Console.Write("Category: ");
            string input = Console.ReadLine();

            if (Enum.TryParse(input, true, out Category category))
                return category;

            Console.WriteLine("Invalid category. Try again.");
        }
    }
}