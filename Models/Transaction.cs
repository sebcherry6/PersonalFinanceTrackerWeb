namespace PersonalFinanceTracker.Models
{

    public class Transaction
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public decimal Amount { get; set; }

        public string Description { get; set; }

        public Category Category { get; set; }

        public TransactionType TransactionType { get; set; }

        public DateTime Date { get; set; } = DateTime.Now;

        public string UserId { get; set; }  // 🔐 CRITICAL SECURITY
    }
}