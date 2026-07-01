using Microsoft.AspNetCore.Mvc;
using PersonalFinanceTracker.Data;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Models.ViewModels;
using PersonalFinanceTracker.Services;
using System.Linq;
using System.Security.Claims;

namespace PersonalFinanceTracker.Web.Controllers
{
    public class TransactionsController : Controller
    {
        private readonly FinanceService _financeService;
        private readonly AppDbContext _context;

        public TransactionsController(AppDbContext context)
        {
            _context = context;
            _financeService = new FinanceService(context);
        }

        private string GetUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier);
        }

        // ---------------- DASHBOARD ----------------
        public IActionResult Dashboard()
        {
            var userId = GetUserId();
            var data = _financeService.GetAll(userId);

            var income = data
    .Where(x => x.TransactionType == TransactionType.Income)
    .Sum(x => x.Amount);

            var expenses = data
                .Where(x => x.TransactionType == TransactionType.Expense)
                .Sum(x => x.Amount);

            var expenseByCategory = data
                .Where(x => x.TransactionType == TransactionType.Expense)
                .GroupBy(x => x.Category)
                .ToDictionary(g => g.Key.ToString(), g => g.Sum(x => x.Amount));

            var model = new DashboardViewModel
            {
                TotalIncome = income,
                TotalExpense = expenses,
                Balance = income - expenses,
                TransactionCount = data.Count,
                RecentTransactions = data.Take(5).ToList(),

                ExpenseByCategory = data
                    .Where(x => x.TransactionType == TransactionType.Expense)
                    .GroupBy(x => x.Category)
                    .ToDictionary(
                        x => x.Key.ToString(),
                        x => x.Sum(t => t.Amount)
                    )
                    
            };

            return View(model);
        }

        // ---------------- INDEX ----------------
        public IActionResult Index()
        {
            var userId = GetUserId();
            return View(_financeService.GetAll(userId));
        }

        // ---------------- CREATE ----------------
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Create(Transaction model)
        {
            model.UserId = GetUserId();

            _financeService.Add(model);

            return RedirectToAction("Index");
        }

        // ---------------- EDIT (GET) ----------------
        [HttpGet]
        public IActionResult Edit(Guid id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var transaction = _financeService
                .GetAll(userId)
                .FirstOrDefault(x => x.Id == id);

            if (transaction == null)
                return NotFound();

            return View(transaction);
        }

        // ---------------- EDIT (POST) ----------------
        [HttpPost]
        public IActionResult Edit(Transaction model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var existing = _financeService
                .GetAll(userId)
                .FirstOrDefault(x => x.Id == model.Id);

            if (existing == null)
                return NotFound();

            existing.Amount = model.Amount;
            existing.Description = model.Description;
            existing.Category = model.Category;
            existing.TransactionType = model.TransactionType;
            existing.Date = model.Date;

            _financeService.Update(existing);

            return RedirectToAction("Index");
        }

        // ---------------- DELETE ----------------
        [HttpGet]
        public IActionResult Delete(Guid id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var transaction = _financeService
                .GetAll(userId)
                .FirstOrDefault(x => x.Id == id);

            if (transaction == null)
                return NotFound();

            return View(transaction);
        }

        [HttpPost]
        public IActionResult DeleteConfirmed(Guid id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var transaction = _financeService
                .GetAll(userId)
                .FirstOrDefault(x => x.Id == id);

            if (transaction == null)
                return NotFound();

            _financeService.Delete(transaction);

            return RedirectToAction("Index");
        }
    }
}