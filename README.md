# 🏦 Bank Management System

A modern, secure, and feature-rich banking application built with **WinUI 3** and **C++** backend.

## ✨ Features

### 🔐 Security
- **SHA-256 + PBKDF2** password hashing (100,000 iterations)
- **Brute-force protection** with account lockout
- **Constant-time comparison** to prevent timing attacks
- **Secure backup codes** for password recovery
- **Session management** with auto-logout

### 👥 User Management
- **Customer Portal**
  - Open Savings/Current accounts
  - Deposit, Withdraw, Transfer funds
  - View transaction history
  - Apply for loans
  - Check account balance

- **Admin Dashboard**
  - Manage all customers
  - Freeze/Unfreeze accounts
  - Approve/Reject loan applications
  - View system-wide transactions
  - Search and filter capabilities

### 💳 Account Features
- Multiple account types (Savings/Current)
- Minimum balance enforcement
- Overdraft protection
- Interest calculation
- Account statements

### 💰 Loan System
- Apply for loans online
- EMI calculation
- Admin approval workflow
- Loan history tracking

### 🎨 Modern UI/UX
- **WinUI 3** with Fluent Design
- **Acrylic/Glass morphism** effects
- Smooth animations and transitions
- Responsive layout
- Dark theme optimized

## 🛡️ Security Features

1. **Password Security**
   - PBKDF2 with 100,000 iterations
   - Per-user salt generation
   - Minimum 8 characters with complexity requirements

2. **Data Protection**
   - Encrypted credential storage
   - Secure session handling
   - Input validation and sanitization

3. **Access Control**
   - Role-based authentication (Customer/Admin)
   - Failed login attempt tracking
   - Automatic lockout after 5 failed attempts

## 📊 Performance

- **Fast data operations** with optimized C++ backend
- **Efficient memory management**
- **Quick search and filtering**
- **Smooth 60 FPS UI animations**

## 🏗️ Technical Stack

**Frontend:**
- WinUI 3 (Windows App SDK)
- XAML for UI
- C# .NET 6+

**Backend:**
- Native C++ DLL
- File-based data persistence
- SHA-256 cryptographic library

**Architecture:**
- MVVM pattern
- P/Invoke interop
- Thread-safe operations

## 📥 Installation

1. Download the latest release (Setup file: ~37.1 MB)
2. Run the installer
3. Follow the installation wizard
4. Launch the application

## 🚀 Quick Start

### For Customers:
1. Click "Sign Up" to create an account
2. Save your **Customer ID** and **Backup Code**
3. Login with your credentials
4. Start banking!

### For Admin:
- **ID:** EMP001
- **Password:** helloboy

## 🔧 Configuration

Data is stored in: `%APPDATA%\BankManagementSystem\`

Files created:
- `users.txt` - Customer data
- `accounts.txt` - Account information
- `transactions.txt` - Transaction history
- `loans.txt` - Loan applications
- `admin_salt.txt` - Admin password salt

## 📝 API Reference

The C++ backend exposes these commands:
- `signup`, `login`, `logout`
- `open_account`, `deposit`, `withdraw`, `transfer`
- `apply_loan`, `approve_loan`, `reject_loan`
- `freeze`, `unfreeze`
- `search`, `list`, `history`

## 🤝 Contributing

This is a demonstration project. For educational purposes only.

## 📄 License

MIT License - Feel free to use for learning!

## 👨‍💻 Developer

**Sudais Ali Shah**

---

**⚠️ Disclaimer:** This is a demonstration/educational project. Not for production use.
