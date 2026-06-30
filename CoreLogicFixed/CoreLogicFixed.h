#pragma once
#include <string>
#include <iostream>
#include <fstream>
#include <sstream>
#include <cmath>
#include <ctime>

using namespace std;

// Forward declaration
class Account;

// ════════════════════════════════════════════════════════════════════════
//  SHA-256 — Pure C++ implementation (no external libraries)
//  Used for password hashing with salt + iteration stretching (PBKDF2-like)
// ════════════════════════════════════════════════════════════════════════
namespace SHA256Impl {
    void        compute(const unsigned char* data, size_t len, unsigned char out[32]);
    std::string hexDigest(const std::string& input);
    // PBKDF2-HMAC-SHA256: 100 000 iterations, 32-byte output → hex string
    std::string pbkdf2(const std::string& password, const std::string& salt,
        int iterations = 100000);
    // Generate a 16-byte hex salt using <random>
    std::string generateSalt();
    // Constant-time compare to prevent timing attacks
    bool        constantTimeEqual(const std::string& a, const std::string& b);
}

// ════════════════════════════════════════════════════════════════════════
//  Person — Base class for Customer and Employee
// ════════════════════════════════════════════════════════════════════════
class Person {
protected:
    string name, cnic, phone, address;
    int age;
public:
    Person(string n, int a, string c, string p, string addr);
    virtual string displayInfo() const = 0;
    string getName()    const;
    string getCNIC()    const;
    string getPhone()   const;
    virtual ~Person();
};

// ════════════════════════════════════════════════════════════════════════
//  Customer — Represents a bank customer with accounts
// ════════════════════════════════════════════════════════════════════════
class Customer : public Person {
    string customerID;
    string backupCodeHash;   // SHA-256(PBKDF2) hash
    string backupCodeSalt;   // per-user salt
    string passwordHash;     // SHA-256(PBKDF2) hash
    string passwordSalt;     // per-user salt
    Account** accounts;
    int accountCount;
public:
    Customer(string id, string n, int a, string c, string p, string addr,
        string backupHash, string backupSalt,
        string pwdHash, string pwdSalt);
    ~Customer();

    void addAccount(Account* acc);
    void closeAccount(const string& accNum);

    string displayInfo() const override;
    bool   operator==(const Customer& other) const;

    string getCustomerID()     const;
    string getBackupCodeHash() const;
    string getBackupCodeSalt() const;
    string getPasswordHash()   const;
    string getPasswordSalt()   const;

    void setPasswordHash(const string& hash);
    void setPasswordSalt(const string& salt);
    void setBackupCodeHash(const string& hash);
    void setBackupCodeSalt(const string& salt);

    bool verifyPassword(const string& pwd)    const;
    bool verifyBackupCode(const string& code) const;

    Account** getAccounts()    const;
    int       getAccountCount() const;
};

// ════════════════════════════════════════════════════════════════════════
//  Employee — Represents bank staff (admin)
// ════════════════════════════════════════════════════════════════════════
class Employee : public Person {
    string employeeID, designation;
    double salary;
public:
    Employee(string id, string n, int a, string c, string p,
        string addr, string des, double sal);

    string displayInfo() const override;
    bool   operator==(const Employee& other) const;
    string getEmployeeID() const;
};

// ════════════════════════════════════════════════════════════════════════
//  Account — Abstract base for SavingsAccount and CurrentAccount
// ════════════════════════════════════════════════════════════════════════
class Account {
protected:
    string accountNumber;
    double balance;
    string status;
    string dateOpened;
public:
    Account(string num, string date);
    virtual ~Account();

    virtual void   withdraw(double amount, string& receipt) = 0;
    virtual string accountType() const = 0;

    void   deposit(double amount, string& receipt);

    double getBalance()       const;
    string getAccountNumber() const;
    string getStatus()        const;
    void   setStatus(string s);
    string getDateOpened()    const { return dateOpened; }

    void setBalance(double b) { balance = b; }
    void setStatusDirect(string s) { status = s; }

    string displayStatement() const;
    bool   operator==(const Account& other) const;
};

// ════════════════════════════════════════════════════════════════════════
//  SavingsAccount
// ════════════════════════════════════════════════════════════════════════
class SavingsAccount : public Account {
    double interestRate, minimumBalance;
public:
    SavingsAccount(string num, string date, double rate, double minBal);

    void   withdraw(double amount, string& receipt) override;
    string accountType() const override;
    void   calculateInterest(string& receipt);

    double getInterestRate()   const { return interestRate; }
    double getMinimumBalance() const { return minimumBalance; }
};

// ════════════════════════════════════════════════════════════════════════
//  CurrentAccount
// ════════════════════════════════════════════════════════════════════════
class CurrentAccount : public Account {
    double overdraftLimit;
public:
    CurrentAccount(string num, string date, double limit);

    void   withdraw(double amount, string& receipt) override;
    string accountType() const override;

    double getOverdraftLimit() const { return overdraftLimit; }
};

// ════════════════════════════════════════════════════════════════════════
//  Transaction
// ════════════════════════════════════════════════════════════════════════
class Transaction {
    string transactionID, type, accountNumber, relatedAccount;
    double amount;
    string date;
public:
    Transaction(string id, string type, double amt, string date,
        string accNum, string related = "");

    string displayReceipt() const;

    string getTransactionID()   const;
    string getType()            const;
    double getAmount()          const;
    string getDate()            const;
    string getAccountNumber()   const;
    string getRelatedAccount()  const;
};

// ════════════════════════════════════════════════════════════════════════
//  Loan
// ════════════════════════════════════════════════════════════════════════
class Loan {
    string loanID, loanCustomerID, loanAccountNumber;
    double principalAmount, interestRate;
    int    tenureMonths;
    string status;
public:
    Loan(string id, string custID, string accNum,
        double p, double r, int t);

    double calculateEMI()        const;
    string displayLoanDetails()  const;

    string getLoanID()          const;
    string getCustomerID()      const;
    string getAccountNumber()   const { return loanAccountNumber; }
    string getStatus()          const;
    void   setStatus(string s);
    double getPrincipalAmount() const;
    double getInterestRate()    const;
    int    getTenureMonths()    const;
};

// ════════════════════════════════════════════════════════════════════════
//  Session
// ════════════════════════════════════════════════════════════════════════
struct Session {
    string userID;
    string role;
    bool   isLoggedIn;
    int    failedAttempts;          // brute-force counter
    time_t lockoutUntil;            // epoch; 0 = not locked
    Session() : isLoggedIn(false), failedAttempts(0), lockoutUntil(0) {}
};

// ════════════════════════════════════════════════════════════════════════
//  Bank — Main class
// ════════════════════════════════════════════════════════════════════════
class Bank {
private:
    Customer** customers;    int customerCount;
    Employee** employees;    int employeeCount;
    Transaction** transactions; int transactionCount;
    Loan** loans;        int loanCount;

    int nextAccountNum, nextTransactionNum, nextLoanNum;

    Session currentSession;
    bool    dataLoaded;

    // Admin credential store (hashed+salted at construction)
    string adminPasswordHash;
    string adminPasswordSalt;

    // ── FIX: base directory where data files live ──
    string dataDir;

    string escapeField(const string& field);
    string unescapeField(const string& field);
    string getCurrentDate();

    void loadData();
    void saveCustomers();   void saveTransactions();
    void saveLoans();       void saveAccounts();
    void loadCustomers();   void loadTransactions();
    void loadLoans();       void loadAccounts();

    bool isAuthenticated(const string& requiredRole) const;
    void initializeCounters();

public:
    Bank();
    ~Bank();

    bool   login(const string& role, const string& id, const string& password);
    bool   signup(const string& name, const string& cnic, const string& phone,
        const string& password, string& backupCode);
    bool resetPassword(const string& customerID, const string& cnic,
        const string& backupCode, const string& newPassword, string& newBackupCode);
    void   logout();

    string getAccountBalance(const string& accNum);
    string listCustomerAccounts(const string& customerId);
    string openAccount(const string& customerID, const string& type,
        double rateOrLimit, double minBal);
    string freezeAccount(const string& accNum);
    string unfreezeAccount(const string& accNum);

    string deposit(const string& accNum, double amount);
    string withdraw(const string& accNum, double amount);
    string transfer(const string& fromAcc, const string& toAcc, double amount);
    string getAccountTransactions(const string& accNum);

    string applyLoan(const string& customerID, const string& accountNumber,
        double principal, double rate, int tenure);
    string approveLoan(const string& loanID);
    string rejectLoan(const string& loanID);
    string getAllPendingLoans();

    // ── NEW: returns all loans belonging to the currently logged-in customer
    string getMyLoans();

    string addCustomer(const string& name, int age, const string& cnic,
        const string& phone, const string& address);
    string searchCustomer(const string& query);
    string displayAllCustomers();

    Customer** getCustomers()      const { return customers; }
    int        getCustomerCount()  const { return customerCount; }
    const Session& getSession()    const { return currentSession; }
};

// ════════════════════════════════════════════════════════════════════════
//  C Export Layer
// ════════════════════════════════════════════════════════════════════════
extern "C" {
    __declspec(dllexport) Bank* __stdcall CreateBank();
    __declspec(dllexport) void         __stdcall DeleteBank(Bank* b);
    __declspec(dllexport) const char* __stdcall ExecuteCommand(
        Bank* b,
        const char* cmd,
        const char* p1,
        const char* p2,
        const char* p3,
        const char* p4);
}