# FinanceTracker by WebCherry

FinanceTracker is a secure web app for managing personal money. People record what comes in and goes out, set monthly budgets, save towards goals, and see at a glance where their money goes. Each person's data is private to their account.


## What your users can do

- **Track money in and out:** add, edit, search and delete income and expenses, each with a category and date.
- **Set monthly budgets:** choose a limit per spending category and see how much is left this month.
- **Save towards goals:** create savings goals and add or withdraw money as they go.
- **See where their money goes:** a monthly overview plus insights covering the last six months.
- **Choose how it looks:** light or dark theme, and their currency symbol (£, € or $).
- **Stay in control of their account:** change their password, sign out on every other device, download all their transactions as a spreadsheet file, or delete their account and everything in it.

## How your users' data is protected

Security is built into every layer, not added at the end:

- **Private by default.** Users can only ever see and change their own records. A request for someone else's data is treated as if the record doesn't exist.
- **Strong sign-in.** Passwords must be at least 12 characters. After 5 wrong attempts, an account pauses for 15 minutes, and repeated sign-in attempts from one place are slowed down. Sign-in takes the same time whether or not an email has an account, so attackers can't use it to discover who's registered.
- **Every page is protected unless deliberately made public.** A new page added later can't be left open by accident.
- **Sessions end when they should.** When a password changes, a user signs out everywhere, or an account is deleted, every existing session and access key stops working immediately.
- **Hardened against common web attacks,** including cross-site request forgery, clickjacking and malicious scripts. The app loads nothing from third-party websites, and pages showing financial data are never cached by the browser.
- **An audit trail.** Sign-ins, failed attempts, lockouts, password changes, sign-outs and account deletions are logged. Logs record user IDs only, never passwords, amounts or the emails used in failed attempts.
- **Continuous scanning.** The code is automatically scanned for security weaknesses on every change and weekly, and the build fails if any third-party component has a known vulnerability.
- **Data rights.** Users can download their data at any time and permanently delete their account and all of its data, supporting your obligations under UK GDPR.

## Trying it on your own computer

You'll need the free [.NET 10 SDK](https://dotnet.microsoft.com/download).

1. Open a terminal in the project folder and run:

   ```
   dotnet restore
   dotnet run --project PersonalFinanceTracker.Web
   ```

2. Open the `https://` address it prints, and create an account.

**If your browser warns about the certificate:** run `dotnet dev-certs https --trust` once and accept the prompt. The app only works over a secure connection.

--------

## Next steps:
1. **Two-step sign-in with an authenticator app.** This doesn't need an email service, so it's the natural next addition.
2. **Email confirmation and password reset.** These need an email provider to be chosen and set up first.
3. **A breached-password check,** which stops users choosing passwords known to have leaked elsewhere.

--------

### Built with

ASP.NET Core 10 MVC, Entity Framework Core, SQLite, ASP.NET Identity, xUnit, Docker and GitHub Actions, with CodeQL code scanning. Results of the code scanning appear under the repository's Security tab.


