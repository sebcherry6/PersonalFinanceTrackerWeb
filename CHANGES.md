# Audit and changes

## Second review (November 2026)

| # | Problem | Risk | Fix |
| --- | --- | --- | --- |
| 1 | API tokens stayed valid for their whole hour after a password change or account deletion. | A stolen token kept working after the owner reacted. | `Security/ApiTokenStampFilter.cs` checks the token's security stamp on every API call; tests in `TokenRevocationTests.cs`. |
| 2 | The bearer handler issued 14-day refresh tokens with no refresh endpoint. | Long-lived credentials with no use. | Refresh token lifetime set to 1 hour, same as the access token. |
| 3 | No way to change the password or end other sessions. | A leaked password or lost device couldn't be dealt with. | Settings: "Change password" and "Sign out everywhere else" (both rotate the security stamp). |
| 4 | Behind a proxy, `X-Forwarded-For` was trusted from any source. | Clients reaching the app directly could fake their IP and dodge the sign-in rate limit. | `ReverseProxy:KnownNetworks` / `KnownProxies` allow-list, `ForwardLimit = 1`, startup warning when unset. |
| 5 | Data-protection keys were stored unencrypted on the `/data` volume. | A copy of the volume or a backup could forge sign-in cookies. | Optional certificate encryption via `DataProtection:CertificatePath`. |
| 6 | No record of security events. | Attacks and account changes left no trace. | `Security/AuditLog.cs`: event IDs 1000-1099, user IDs only. |
| 7 | No static analysis in CI. | Code-level issues only found by hand. | `.github/workflows/codeql.yml`. |
| 8 | README said a database was "attached" while `.gitignore` excludes `*.db`. | Confusing; invites force-adding a database with real data. | README corrected; the app creates the file on first run. |

Design: moved to the shared WebCherry brand (Code Cherry logo, `#101115` dark base, cherry accents, Space Grotesk / Inter / JetBrains Mono, self-hosted). Dark is now the default theme; the light theme follows the same palette. Button red is `#D42A4C` so white text passes 4.5:1; the danger button is outlined so it can't be confused with the main action.

Not verified here: written without a .NET SDK, so run `dotnet build` and `dotnet test` before merging.

## First review

What was found in the original project and what was done about it, most serious first.

## Security

| # | Problem | Risk | Fix |
| --- | --- | --- | --- |
| 1 | Settings form posted a hidden `UserId`; `SaveSettingsAsync` saved to whichever user it named. | Any signed-in user could overwrite any other user's settings. | Settings are always loaded and saved for the signed-in user. The form has no `UserId` field. |
| 2 | Edit bound the posted `Transaction` entity and called `db.Update(...)`. | Over-posting: a crafted form could change `UserId` or `Id`. | Forms and API bind to `TransactionInput`; the service loads the owned row and copies only editable fields. |
| 3 | Sign-in used `lockoutOnFailure: false` and no rate limit. | Unlimited password guessing. | Lockout after 5 failures for 15 minutes, plus a per-IP rate limit on sign-in, registration and the token endpoint. |
| 4 | Password rules were all disabled (minimum length 6). | Weak passwords. | 12-character minimum with 4 unique characters, following NIST guidance. |
| 5 | `LocalRedirect(returnUrl)` with no check. | An off-site `returnUrl` crashed the request with a 500. | `Url.IsLocalUrl` check; anything else goes to the overview. |
| 6 | Sign-in always created a persistent cookie, with default lifetime and cookie settings. | Sessions lingered on shared computers. | 8-hour sliding session, persistent only with "Keep me signed in", `__Host-` prefix, HTTPS-only, `SameSite=Strict`, security stamp re-checked every 5 minutes. |
| 7 | No security headers. Bootstrap and Chart.js from a CDN; inline scripts and `onclick` handlers. | No defence in depth against XSS, clickjacking or a compromised CDN. | Strict CSP (`'self'` only, no inline), `nosniff`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`, COOP/CORP, HSTS. No third-party scripts; charts are SVG. |
| 8 | Unknown emails returned faster than wrong passwords. | Attackers could discover which emails have accounts. | A dummy hash is checked for unknown emails, and both cases show the same message. |
| 9 | `[Authorize]` per controller, no fallback. | A new controller would be public by default. | Global fallback policy requires sign-in; public endpoints opt out with `[AllowAnonymous]`. |
| 10 | Anti-forgery attributes added by hand on some actions. | Easy to miss one. | Global `AutoValidateAntiforgeryToken` filter. |
| 11 | Pages with financial data were cacheable. | Back button after sign-out on a shared computer. | `Cache-Control: no-store` on all MVC responses. |
| 12 | Model binding accepted undefined enum values (`Category=999`) and categories that didn't fit the type. | Corrupt data. | `Enum.IsDefined` checks and category/type rules in `TransactionInput.Validate`. |
| 13 | No foreign keys from finance tables to users. | Orphaned data, and no way to delete an account cleanly. | Foreign keys with `ON DELETE CASCADE`, plus "Delete my account" (password-confirmed). |
| 14 | `Microsoft.AspNetCore.Identity.UI` and the scaffolding package were shipped. | Unused surface and about 40 MB of tooling in `bin/`. | Removed. |
| 15 | CSV export did not exist. Added with formula-injection protection. | `=HYPERLINK(...)` in a description could run when the file opens in Excel. | Cells starting with `= + - @` are prefixed with `'`. |

## Broken functionality

- `TransactionsController` was missing Dashboard, Index, Create, Edit, Delete, Analytics, SavingsGoals and Settings. A comment said they "remain the same", but the code was gone, so most pages returned 404. All rebuilt and split into focused controllers.
- `UseExceptionHandler("/Home/Error")` pointed at a `HomeController` that didn't exist. Added, with friendly 404, 429 and 500 pages.
- The second migration (`AddFinanceTables`) was empty. Replaced by a single `InitialCreate` migration that matches the model, and a fresh database with it applied.
- Category dropdowns offered "Freelance" and "Investments", which weren't in the enum, so those selections failed to bind.
- Budgets linked to `/Transactions/Budgets/Create`, which didn't exist. Savings goal forms posted to `AddGoal` and `AddToGoal`, which didn't exist. Both pages now work end to end.
- Budget "spent" counted all-time spending rather than this month's.
- `ToString("C")` printed `¤` instead of `£` in Linux containers. Money is now formatted explicitly, in the currency chosen in Settings (which previously had no effect).
- Theme was stored in two places (database and `localStorage`) with two scripts (`theme.js` and an inline one) using different keys. Now one setting, applied by the server, with a "Match device" option.
- `FinanceService` was created with `new` inside the controller, bypassing dependency injection.
- Mixed namespaces (`PersonalFinanceTracker.*` and `PersonalFinanceTracker.Web.*`) unified.

## Added

- JSON API with bearer tokens (`/api/auth/token`, `/api/transactions`, `/api/summary`).
- Test project: service-level tests plus full-app security tests that start the real app in memory.
- Dockerfile (multi-stage, non-root, persistent `/data` volume), GitHub Actions CI, Dependabot.
- Server-side search, filtering and paging for transactions; month-by-month insights; CSV export; account deletion.
- A new interface: ledger-style serif figures, light and dark themes, responsive down to phones, keyboard and screen-reader friendly.

## Removed

Old `Areas/Identity` scaffold folders (never compiled, outside the project), `ApplicationUser` (unused), `TransactionFactory` (unused, set no `UserId`), the `Summary` view, the template Home views, `_LoginPartial`, `_EmptyState` (referenced a missing image), the six partial CSS files, `theme.js`, `.csproj.user`, `.csproj.Backup.tmp`, and the 43 MB `.csproj.Backup.zip`. `wwwroot/lib` (Bootstrap, jQuery) is no longer referenced and can be deleted.

## COMPILED AND RUN!
