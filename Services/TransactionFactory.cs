using System;
using PersonalFinanceTracker.Models;

namespace PersonalFinanceTracker.Services
{
    public static class TransactionFactory
    {
        public static Transaction Create(
            TransactionType type,
            decimal amount,
            string description,
            Category category)
        {
            return new Transaction
            {
                Id = Guid.NewGuid(),
                Amount = amount,
                Description = description,
                Category = category,
                Date = DateTime.Now,
                TransactionType = type
            };
        }
    }
}