using System.Collections.Generic;

namespace PersonalFinanceTracker.Models.ViewModels
{
    public class DashboardViewModel
    {
        public decimal TotalIncome { get; set; }
        public decimal TotalExpense { get; set; }
        public decimal Balance { get; set; }
        public int TransactionCount { get; set; }

        public List<Transaction> RecentTransactions { get; set; } = new();

        public Dictionary<string, decimal> ExpenseByCategory { get; set; } = new();
    }
}