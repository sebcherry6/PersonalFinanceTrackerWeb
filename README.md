# FinanceTracker by WebCherry

A personal finance tracker: record money in and out, set monthly budgets per category, save towards goals, and see where your money goes. Built with ASP.NET Core 10 MVC, Entity Framework Core, SQLite and ASP.NET Identity, with a JSON API, automated tests, a Docker image and a CI pipeline.

## What's in the box

| Part | Where |
| --- | --- |
| Web app (MVC + Razor views) | `PersonalFinanceTracker.Web/` |
| Database (SQLite, created on first run, never committed) | `PersonalFinanceTracker.Web/App_Data/finance.db` |
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

Open the HTTPS address it prints and create an account. The database file is created on the first start; delete it any time to start fresh.

**Brand fonts (once):** the app uses the WebCherry fonts (Space Grotesk, Inter, JetBrains Mono) and serves them itself, because the Content-Security-Policy blocks font CDNs. Download them with:

```
pwsh tools/get-fonts.ps1
```

Without them the app still works and falls back to your system fonts.

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

Things that matter on a real host:

1. **Persistent storage at `/data`.** The database and the data-protection keys live there. Without a volume, every redeploy wipes all accounts and signs everyone out.
2. **HTTPS in front of the container.** Every cloud host above provides it. Then set the environment variable `ReverseProxy__Enabled=true` so the app sees real client IPs (for rate limiting) and the HTTPS scheme (for HSTS).
3. **Health probe** on `GET /healthz`.
4. **Tell the app which proxy to trust.** Set `ReverseProxy__KnownNetworks__0` to your host's internal range (e.g. `10.0.0.0/8`) or `ReverseProxy__KnownProxies__0` to the proxy's IP. Without it the app trusts whichever proxy is in front of it and logs a warning at startup.
5. **Encrypt the sign-in keys.** Give the app a certificate with `DataProtection__CertificatePath` and `DataProtection__CertificatePassword` (keep the password in the host's secret store). Otherwise anyone with a copy of the `/data` volume could forge sign-in cookies.
6. **Lock the host name.** Set `AllowedHosts` to your domain, e.g. `AllowedHosts=finance.webcherry.dev`.
7. **Run the container locked down** if your host lets you pass flags:

```
docker run -p 8080:8080 -v financetracker-data:/data \
  --read-only --tmpfs /tmp --cap-drop ALL --security-opt no-new-privileges \
  webcherry/financetracker
```

SQLite suits a single instance. To run several instances, switch to Azure SQL or PostgreSQL: change `UseSqlite` in `Program.cs` to the matching provider, regenerate the migration, and run migrations as a deploy step rather than on startup.

## Security, in brief

Every protection is commented where it's implemented. The main ones:

- **Per-user data isolation.** Every query in `Services/FinanceService.cs` filters by the signed-in user's ID. Another user's record returns 404, exactly like one that doesn't exist.
- **No over-posting.** Forms and the API bind to input models that have no `Id` or `UserId` field; the server sets those.
- **Authentication.** 12+ character passwords, lockout after 5 failures for 15 minutes, timing-safe sign-in (unknown emails take as long as wrong passwords), rate limiting on sign-in, registration and token endpoints, and short-lived HTTPS-only `__Host-` cookies with `SameSite=Strict`.
- **Secure by default.** A fallback policy requires sign-in on every endpoint unless it's explicitly marked public.
- **CSRF.** Anti-forgery tokens are required on every form post. The API accepts bearer tokens only, never cookies.
- **Browser hardening.** A strict Content-Security-Policy with no inline scripts or styles and no third-party CDNs, plus HSTS, `nosniff`, frame blocking, and `no-store` caching on pages with financial data.
- **Tokens die when they should.** API tokens carry the account's security stamp and are refused as soon as the password changes, you sign out everywhere, or the account is deleted (not an hour later).
- **Password and sessions.** Change your password and "Sign out everywhere else" from Settings.
- **Audit log.** Sign-ins, failures, lockouts, password changes, sign-outs, deletions and rejected tokens are logged with event IDs 1000-1099 (user ID only, never passwords, emails on failures, or amounts). Filter on those IDs in your host's log viewer.
- **Code scanning.** CodeQL runs on every push and weekly; results appear under the repo's Security tab.
- **Data rights.** Users can download their data as CSV (protected against spreadsheet formula injection) and delete their account, which removes all of their data through database cascades.

Not done yet, and worth adding before real users: two-factor sign-in with an authenticator app (needs no email provider, so it's next), email confirmation and password reset (needs an email provider), and a breached-password check.

## Testing it by hand

There's a security test log with 42 step-by-step checks (curl/PowerShell commands, DevTools steps, expected results and a Pass/Fail column). Work through it after any change to sign-in, the API or the Docker setup.

## Project layout

```
PersonalFinanceTracker.Web/
  Program.cs                startup: services, security settings, request pipeline
  Controllers/              MVC pages (Auth, Dashboard, Transactions, Budgets, Goals, Settings)
  Controllers/Api/          JSON API
  Data/                     EF Core context and migrations
  Models/                   entities and validated input models
  Services/                 FinanceService (all data access), preferences, sign-in, CSV export
  Security/                 headers, rate limiting, auth helpers, API token checks, audit log
  Views/                    Razor pages
  wwwroot/                  site.css (WebCherry brand), site.js, Code Cherry logo, fonts/
tools/get-fonts.ps1         downloads the brand fonts into wwwroot/fonts
```

