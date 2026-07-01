using PersonalFinanceTracker.Data;
using PersonalFinanceTracker.Models;

namespace PersonalFinanceTracker.Services
{
    public class FinanceService
    {
        private readonly AppDbContext _context;

        public FinanceService(AppDbContext context)
        {
            _context = context;
        }

        public List<Transaction> GetAll(string userId)
        {
            return _context.Transactions
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.Date)
                .ToList();
        }

        public void Add(Transaction t)
        {
            _context.Transactions.Add(t);
            _context.SaveChanges();
        }

        public void Update(Transaction t)
        {
            _context.Transactions.Update(t);
            _context.SaveChanges();
        }

        public void Delete(Transaction t)
        {
            _context.Transactions.Remove(t);
            _context.SaveChanges();
        }
    }
}