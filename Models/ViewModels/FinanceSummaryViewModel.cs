namespace PersonalFinanceTracker.Models.ViewModels
{
    public class FinanceSummaryViewModel
    {
        public decimal TotalIncome { get; set; }
        public decimal TotalExpense { get; set; }
        public decimal Balance { get; set; }
        public int TransactionCount { get; set; }
    }
}