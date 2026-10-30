# FinanceTracker by WebCherry

A personal finance tracker: record money in and out, set monthly budgets per category, save towards goals, and see where your money goes. Built with ASP.NET Core 10 MVC, Entity Framework Core, SQLite and ASP.NET Identity, with a JSON API, automated tests, a Docker image and a CI pipeline.

## What's in the box

| Part | Where |
| --- | --- |
| Web app (MVC + Razor views) | `PersonalFinanceTracker.Web/` |
| Database (SQLite, attached and ready) | `PersonalFinanceTracker.Web/App_Data/finance.db` |
| Schema migration | `PersonalFinanceTracker.Web/Data/Migrations/` |
| JSON API | `PersonalFinanceTracker.Web/Controllers/Api/` |
| Tests (xUnit, unit + full-app security tests) | `PersonalFinanceTracker.Tests/` |
| Container image | `Dockerfile` |
| CI (build, test, vulnerability scan, migration check, image build) | `.github/workflows/ci.yml` |

## Run it locally

You need the .NET 10 SDK.

```bash
dotnet restore
dotnet run --project PersonalFinanceTracker.Web
```

Open the HTTPS address it prints and create an account. The attached database is empty (schema only, no users). If you delete it, the app recreates it on the next start.

First time on this machine? Trust the development HTTPS certificate once with `dotnet dev-certs https --trust`. The sign-in cookie is HTTPS-only.

## Run the tests

```bash
dotnet test
```

The security tests start the whole app in memory with a throwaway database and check that the protections actually hold: requests without a token are refused, one user cannot read, edit or delete another user's transaction, five wrong passwords lock the account, form posts without an anti-forgery token are rejected, and the security headers are present.

## The API

Get a token, then send it as a bearer header. Tokens last one hour.

```bash
curl -X POST https://localhost:7163/api/auth/token \
  -H "Content-Type: application/json" \
  -d '{"email":"you@example.com","password":"your password"}'
# -> { "tokenType": "Bearer", "accessToken": "...", "expiresIn": 3600, ... }

curl https://localhost:7163/api/transactions -H "Authorization: Bearer <accessToken>"
```

| Method | Path | Does |
| --- | --- | --- |
| POST | `/api/auth/token` | Exchange email and password for a token |
| GET | `/api/transactions?search=&type=Expense&page=1` | List (25 per page) |
| GET | `/api/transactions/{id}` | One transaction |
| POST | `/api/transactions` | Create: `{ description, amount, type, category, date }` |
| PUT | `/api/transactions/{id}` | Replace |
| DELETE | `/api/transactions/{id}` | Delete |
| GET | `/api/summary` | Balance and this month's totals |

`type` is `Income` or `Expense`. `category` must suit the type (for example `Salary` for income, `Food` for expenses, `Other` for either).

## Deploy to the cloud

The app ships as a container. Any host that runs containers works (Azure Container Apps, Azure App Service for Containers, Fly.io, Render, a VPS).

```bash
docker build -t webcherry/financetracker .
docker run -p 8080:8080 -v financetracker-data:/data webcherry/financetracker
```

Three things matter on a real host:

1. **Persistent storage at `/data`.** The database and the data-protection keys live there. Without a volume, every redeploy wipes all accounts and signs everyone out.
2. **HTTPS in front of the container.** Every cloud host above provides it. Then set the environment variable `ReverseProxy__Enabled=true` so the app sees real client IPs (for rate limiting) and the HTTPS scheme (for HSTS).
3. **Health probe** on `GET /healthz`.

SQLite suits a single instance. To run several instances, switch to Azure SQL or PostgreSQL: change `UseSqlite` in `Program.cs` to the matching provider, regenerate the migration, and run migrations as a deploy step rather than on startup.

## Security, in brief

Every protection is commented where it's implemented. The main ones:

- **Per-user data isolation.** Every query in `Services/FinanceService.cs` filters by the signed-in user's ID. Another user's record returns 404, exactly like one that doesn't exist.
- **No over-posting.** Forms and the API bind to input models that have no `Id` or `UserId` field; the server sets those.
- **Authentication.** 12+ character passwords, lockout after 5 failures for 15 minutes, timing-safe sign-in (unknown emails take as long as wrong passwords), rate limiting on sign-in, registration and token endpoints, and short-lived HTTPS-only `__Host-` cookies with `SameSite=Strict`.
- **Secure by default.** A fallback policy requires sign-in on every endpoint unless it's explicitly marked public.
- **CSRF.** Anti-forgery tokens are required on every form post. The API accepts bearer tokens only, never cookies.
- **Browser hardening.** A strict Content-Security-Policy with no inline scripts or styles and no third-party CDNs, plus HSTS, `nosniff`, frame blocking, and `no-store` caching on pages with financial data.
- **Data rights.** Users can download their data as CSV (protected against spreadsheet formula injection) and delete their account, which removes all of their data through database cascades.

Not done yet, and worth adding before real users: email confirmation and password reset (needs an email provider), two-factor authentication, and an optional breached-password check.

## Project layout

```
PersonalFinanceTracker.Web/
  Program.cs                startup: services, security settings, request pipeline
  Controllers/              MVC pages (Auth, Dashboard, Transactions, Budgets, Goals, Settings)
  Controllers/Api/          JSON API
  Data/                     EF Core context and migrations
  Models/                   entities and validated input models
  Services/                 FinanceService (all data access), preferences, sign-in, CSV export
  Security/                 headers, rate limiting, auth helpers
  Views/                    Razor pages
  wwwroot/                  site.css, site.js, logo
```

## Repository note

Your Git repository was inside the project folder, so the solution file, tests and Dockerfile sat outside it. Move `.git` up one level so the repository root is this folder (the one containing `PersonalFinanceTracker.Web.slnx`). The CI workflow expects that layout.
