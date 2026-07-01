# 💰 FinanceTracker by WebCherry

A modern fintech-style personal finance tracker built with ASP.NET Core MVC.

---

## 🍒 Overview

FinanceTracker by WebCherry is a clean and modern web application that helps users track:

- Income 💰
- Expenses 💸
- Balance 📊
- Transaction history 📋

It features a **Revolut-style dashboard UI**, dark/light mode, and secure authentication.

---

## 🚀 Features

### 🔐 Authentication
- ASP.NET Core Identity login/register system
- Secure per-user data isolation

### 💰 Finance Tracking
- Add / Edit / Delete transactions
- Income vs Expense tracking
- Category-based organization

### 📊 Dashboard
- Balance overview
- Income vs Expenses chart
- Recent transactions
- Category insights

### 🎨 UI / UX
- Fintech dark/light mode 🌗
- Responsive sidebar layout
- Clean dashboard cards
- Chart.js analytics

---

## 🧠 Tech Stack

- ASP.NET Core MVC
- Entity Framework Core
- SQLite Database
- ASP.NET Identity
- Chart.js
- Bootstrap 5

---

## 🏗️ Architecture

Controllers → Services → Database (EF Core)
Views → Razor UI
Identity → Authentication Layer

---

## 📦 How to Run

```bash
dotnet restore
dotnet ef database update
dotnet run