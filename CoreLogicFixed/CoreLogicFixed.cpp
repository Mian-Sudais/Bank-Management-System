#define _CRT_SECURE_NO_WARNINGS
#include "pch.h"
#include "CoreLogicFixed.h"
#include <string>
#include <iostream>
#include <fstream>
#include <sstream>
#include <cmath>
#include <ctime>
#include <cctype>
#include <random>
#include <windows.h>
#include <shlobj.h>
#pragma comment(lib, "shell32.lib")
#include <iomanip>

using namespace std;

static string ResolveDataDirectory()
{
    char appDataPath[MAX_PATH] = {};
    if (FAILED(SHGetFolderPathA(NULL, CSIDL_APPDATA, NULL, 0, appDataPath)))
    {
        const char* env = getenv("APPDATA");
        if (env) strncpy(appDataPath, env, MAX_PATH - 1);
        else     strncpy(appDataPath, ".", MAX_PATH - 1);
    }

    string dataDir = string(appDataPath) + "\\BankManagementSystem\\";
    CreateDirectoryA(dataDir.c_str(), NULL);
    return dataDir;
}

inline double roundMoney(double val) {
    return std::round(val * 100.0) / 100.0;
}

inline std::string formatMoney(double val) {
    std::ostringstream oss;
    oss << std::fixed << std::setprecision(2) << roundMoney(val);
    return oss.str();
}

namespace SHA256Impl {

    static const uint32_t K[64] = {
        0x428a2f98,0x71374491,0xb5c0fbcf,0xe9b5dba5,
        0x3956c25b,0x59f111f1,0x923f82a4,0xab1c5ed5,
        0xd807aa98,0x12835b01,0x243185be,0x550c7dc3,
        0x72be5d74,0x80deb1fe,0x9bdc06a7,0xc19bf174,
        0xe49b69c1,0xefbe4786,0x0fc19dc6,0x240ca1cc,
        0x2de92c6f,0x4a7484aa,0x5cb0a9dc,0x76f988da,
        0x983e5152,0xa831c66d,0xb00327c8,0xbf597fc7,
        0xc6e00bf3,0xd5a79147,0x06ca6351,0x14292967,
        0x27b70a85,0x2e1b2138,0x4d2c6dfc,0x53380d13,
        0x650a7354,0x766a0abb,0x81c2c92e,0x92722c85,
        0xa2bfe8a1,0xa81a664b,0xc24b8b70,0xc76c51a3,
        0xd192e819,0xd6990624,0xf40e3585,0x106aa070,
        0x19a4c116,0x1e376c08,0x2748774c,0x34b0bcb5,
        0x391c0cb3,0x4ed8aa4a,0x5b9cca4f,0x682e6ff3,
        0x748f82ee,0x78a5636f,0x84c87814,0x8cc70208,
        0x90befffa,0xa4506ceb,0xbef9a3f7,0xc67178f2
    };

    static inline uint32_t rotr(uint32_t x, int n) { return (x >> n) | (x << (32 - n)); }
    static inline uint32_t ch(uint32_t e, uint32_t f, uint32_t g) { return (e & f) ^ (~e & g); }
    static inline uint32_t maj(uint32_t a, uint32_t b, uint32_t c) { return (a & b) ^ (a & c) ^ (b & c); }
    static inline uint32_t sig0(uint32_t x) { return rotr(x, 2) ^ rotr(x, 13) ^ rotr(x, 22); }
    static inline uint32_t sig1(uint32_t x) { return rotr(x, 6) ^ rotr(x, 11) ^ rotr(x, 25); }
    static inline uint32_t eps0(uint32_t x) { return rotr(x, 7) ^ rotr(x, 18) ^ (x >> 3); }
    static inline uint32_t eps1(uint32_t x) { return rotr(x, 17) ^ rotr(x, 19) ^ (x >> 10); }

    void compute(const unsigned char* data, size_t len, unsigned char out[32])
    {
        uint32_t h0 = 0x6a09e667, h1 = 0xbb67ae85, h2 = 0x3c6ef372, h3 = 0xa54ff53a;
        uint32_t h4 = 0x510e527f, h5 = 0x9b05688c, h6 = 0x1f83d9ab, h7 = 0x5be0cd19;

        size_t bitlen = len * 8;
        size_t padLen = (len % 64 < 56) ? (56 - len % 64) : (120 - len % 64);
        size_t msgLen = len + padLen + 8;

        unsigned char* msg = new unsigned char[msgLen]();
        for (size_t i = 0; i < len; ++i) msg[i] = data[i];
        msg[len] = 0x80;
        for (int i = 0; i < 8; ++i)
            msg[msgLen - 1 - i] = static_cast<unsigned char>((bitlen >> (8 * i)) & 0xFF);

        for (size_t i = 0; i < msgLen; i += 64) {
            uint32_t w[64];
            for (int j = 0; j < 16; ++j)
                w[j] = (uint32_t(msg[i + j * 4]) << 24) | (uint32_t(msg[i + j * 4 + 1]) << 16) |
                (uint32_t(msg[i + j * 4 + 2]) << 8) | uint32_t(msg[i + j * 4 + 3]);
            for (int j = 16; j < 64; ++j)
                w[j] = eps1(w[j - 2]) + w[j - 7] + eps0(w[j - 15]) + w[j - 16];

            uint32_t a = h0, b = h1, c = h2, d = h3, e = h4, f = h5, g = h6, h = h7;
            for (int j = 0; j < 64; ++j) {
                uint32_t t1 = h + sig1(e) + ch(e, f, g) + K[j] + w[j];
                uint32_t t2 = sig0(a) + maj(a, b, c);
                h = g; g = f; f = e; e = d + t1;
                d = c; c = b; b = a; a = t1 + t2;
            }
            h0 += a; h1 += b; h2 += c; h3 += d;
            h4 += e; h5 += f; h6 += g; h7 += h;
        }
        delete[] msg;

        uint32_t H[8] = { h0,h1,h2,h3,h4,h5,h6,h7 };
        for (int i = 0; i < 8; ++i) {
            out[i * 4 + 0] = (H[i] >> 24) & 0xFF;
            out[i * 4 + 1] = (H[i] >> 16) & 0xFF;
            out[i * 4 + 2] = (H[i] >> 8) & 0xFF;
            out[i * 4 + 3] = H[i] & 0xFF;
        }
    }

    string hexDigest(const string& input)
    {
        unsigned char out[32];
        compute(reinterpret_cast<const unsigned char*>(input.data()), input.size(), out);
        static const char hex[] = "0123456789abcdef";
        string result(64, ' ');
        for (int i = 0; i < 32; ++i) {
            result[i * 2] = hex[(out[i] >> 4) & 0xF];
            result[i * 2 + 1] = hex[out[i] & 0xF];
        }
        return result;
    }

    static void hmac(const unsigned char* key, size_t keyLen,
        const unsigned char* msg, size_t msgLen,
        unsigned char out[32])
    {
        unsigned char kpad[64] = {};
        unsigned char normKey[32];
        if (keyLen > 64) { compute(key, keyLen, normKey); key = normKey; keyLen = 32; }
        for (size_t i = 0; i < keyLen; ++i) kpad[i] = key[i];

        unsigned char ipad[64], opad[64];
        for (int i = 0; i < 64; ++i) { ipad[i] = kpad[i] ^ 0x36; opad[i] = kpad[i] ^ 0x5c; }

        size_t innerLen = 64 + msgLen;
        unsigned char* inner = new unsigned char[innerLen];
        for (int i = 0; i < 64; ++i) inner[i] = ipad[i];
        for (size_t i = 0; i < msgLen; ++i) inner[64 + i] = msg[i];
        unsigned char innerHash[32];
        compute(inner, innerLen, innerHash);
        delete[] inner;

        unsigned char outer[64 + 32];
        for (int i = 0; i < 64; ++i) outer[i] = opad[i];
        for (int i = 0; i < 32; ++i) outer[64 + i] = innerHash[i];
        compute(outer, 64 + 32, out);
    }

    string pbkdf2(const string& password, const string& salt, int iterations)
    {
        const unsigned char* pass = reinterpret_cast<const unsigned char*>(password.data());
        size_t passLen = password.size();

        string saltInt = salt + "\x00\x00\x00\x01";
        const unsigned char* s = reinterpret_cast<const unsigned char*>(saltInt.data());

        unsigned char u[32], result[32];
        hmac(pass, passLen, s, saltInt.size(), u);
        for (int i = 0; i < 32; ++i) result[i] = u[i];

        for (int iter = 1; iter < iterations; ++iter) {
            unsigned char uNext[32];
            hmac(pass, passLen, u, 32, uNext);
            for (int i = 0; i < 32; ++i) { result[i] ^= uNext[i]; u[i] = uNext[i]; }
        }

        static const char hex[] = "0123456789abcdef";
        string out(64, ' ');
        for (int i = 0; i < 32; ++i) {
            out[i * 2] = hex[(result[i] >> 4) & 0xF];
            out[i * 2 + 1] = hex[result[i] & 0xF];
        }
        return out;
    }

    string generateSalt()
    {
        static random_device rd;
        static mt19937_64 gen(rd());
        static uniform_int_distribution<uint64_t> dist;
        uint64_t a = dist(gen), b = dist(gen);
        static const char hex[] = "0123456789abcdef";
        string s(32, ' ');
        for (int i = 0; i < 8; ++i) {
            s[i * 2] = hex[(a >> (i * 8 + 4)) & 0xF];
            s[i * 2 + 1] = hex[(a >> (i * 8)) & 0xF];
        }
        for (int i = 0; i < 8; ++i) {
            s[16 + i * 2] = hex[(b >> (i * 8 + 4)) & 0xF];
            s[16 + i * 2 + 1] = hex[(b >> (i * 8)) & 0xF];
        }
        return s;
    }

    bool constantTimeEqual(const string& a, const string& b)
    {
        if (a.size() != b.size()) return false;
        unsigned char diff = 0;
        for (size_t i = 0; i < a.size(); ++i)
            diff |= static_cast<unsigned char>(a[i]) ^ static_cast<unsigned char>(b[i]);
        return diff == 0;
    }

} // namespace SHA256Impl

static bool isValidDigits(const string& str, size_t expectedLength)
{
    if (str.length() != expectedLength) return false;
    for (char c : str)
        if (!isdigit(static_cast<unsigned char>(c))) return false;
    return true;
}

static string getCurrentDate()
{
    time_t now = time(nullptr);
    tm* ti = localtime(&now);
    char buf[20];
    strftime(buf, sizeof(buf), "%Y-%m-%d %H:%M:%S", ti);
    return string(buf);
}

static string escapeField(const string& field)
{
    string r;
    for (char c : field) {
        if (c == '|')  r += "\\|";
        else if (c == '\\') r += "\\\\";
        else r += c;
    }
    return r;
}

static string unescapeField(const string& field)
{
    string r;
    for (size_t i = 0; i < field.size(); ++i) {
        if (field[i] == '\\' && i + 1 < field.size() &&
            (field[i + 1] == '|' || field[i + 1] == '\\'))
        {
            r += field[i + 1]; ++i;
        }
        else r += field[i];
    }
    return r;
}

Person::Person(string n, int a, string c, string p, string addr)
    : name(n), age(a), cnic(c), phone(p), address(addr) {}
string Person::getName()  const { return name; }
string Person::getCNIC()  const { return cnic; }
string Person::getPhone() const { return phone; }
Person::~Person() {}

Customer::Customer(string id, string n, int a, string c, string p, string addr,
    string backupHash, string backupSalt,
    string pwdHash, string pwdSalt)
    : Person(n, a, c, p, addr),
    customerID(id),
    backupCodeHash(backupHash), backupCodeSalt(backupSalt),
    passwordHash(pwdHash), passwordSalt(pwdSalt)
{
    accounts = new Account * [100]();
    accountCount = 0;
}

Customer::~Customer()
{
    for (int i = 0; i < accountCount; ++i) delete accounts[i];
    delete[] accounts;
}

void Customer::addAccount(Account* acc)
{
    if (accountCount < 100 && acc) accounts[accountCount++] = acc;
}

void Customer::closeAccount(const string& accNum)
{
    for (int i = 0; i < accountCount; ++i) {
        if (accounts[i] && accounts[i]->getAccountNumber() == accNum) {
            delete accounts[i];
            for (int j = i; j < accountCount - 1; ++j) accounts[j] = accounts[j + 1];
            accounts[--accountCount] = nullptr;
            return;
        }
    }
}

string Customer::displayInfo() const
{
    stringstream ss;
    ss << customerID << " | " << name << " | " << cnic;
    return ss.str();
}

bool   Customer::operator==(const Customer& o) const { return customerID == o.customerID; }
string Customer::getCustomerID()     const { return customerID; }
string Customer::getBackupCodeHash() const { return backupCodeHash; }
string Customer::getBackupCodeSalt() const { return backupCodeSalt; }
string Customer::getPasswordHash()   const { return passwordHash; }
string Customer::getPasswordSalt()   const { return passwordSalt; }
void   Customer::setPasswordHash(const string& h) { passwordHash = h; }
void   Customer::setPasswordSalt(const string& s) { passwordSalt = s; }
void Customer::setBackupCodeHash(const string& hash) { backupCodeHash = hash; }
void Customer::setBackupCodeSalt(const string& salt) { backupCodeSalt = salt; }

bool Customer::verifyPassword(const string& pwd) const
{
    if (passwordSalt.empty() || passwordHash.empty()) return false;
    string computed = SHA256Impl::pbkdf2(pwd, passwordSalt);
    return SHA256Impl::constantTimeEqual(computed, passwordHash);
}

bool Customer::verifyBackupCode(const string& code) const
{
    if (backupCodeSalt.empty() || backupCodeHash.empty()) return false;
    string computed = SHA256Impl::pbkdf2(code, backupCodeSalt);
    return SHA256Impl::constantTimeEqual(computed, backupCodeHash);
}

Account** Customer::getAccounts()     const { return accounts; }
int       Customer::getAccountCount() const { return accountCount; }

Employee::Employee(string id, string n, int a, string c, string p,
    string addr, string des, double sal)
    : Person(n, a, c, p, addr), employeeID(id), designation(des), salary(sal) {}

string Employee::displayInfo() const
{
    stringstream ss;
    ss << "ID: " << employeeID << " | " << name << " | " << designation;
    return ss.str();
}

bool   Employee::operator==(const Employee& o) const { return employeeID == o.employeeID; }
string Employee::getEmployeeID() const { return employeeID; }

Account::Account(string num, string date)
    : accountNumber(num), balance(0.0), status("active"), dateOpened(date) {}
Account::~Account() {}

void Account::deposit(double amount, string& receipt) {
    if (amount <= 0 || status != "active") {
        receipt = "Deposit failed: Invalid amount or account frozen."; return;
    }
    balance = roundMoney(balance + amount);
    receipt = "Deposited: " + formatMoney(amount) + " to " + accountNumber
        + " | New Balance: " + formatMoney(balance);
}

double Account::getBalance()       const { return balance; }
string Account::getAccountNumber() const { return accountNumber; }
string Account::getStatus()        const { return status; }
void   Account::setStatus(string s) { status = s; }

string Account::displayStatement() const {
    std::ostringstream ss;
    ss << accountNumber << " | " << accountType()
        << " | Balance: $" << formatMoney(balance)
        << " | " << (status == "frozen" ? "Frozen" : "Active")
        << " | Opened: " << dateOpened;
    return ss.str();
}

bool Account::operator==(const Account& o) const
{
    return accountNumber == o.accountNumber;
}

SavingsAccount::SavingsAccount(string num, string date, double rate, double minBal)
    : Account(num, date), interestRate(rate), minimumBalance(minBal) {}

void SavingsAccount::withdraw(double amount, string& receipt) {
    if (amount <= 0 || status != "active") {
        receipt = "Withdrawal failed: Invalid amount or account frozen."; return;
    }
    double newBal = roundMoney(balance - amount);
    if (newBal < minimumBalance) {
        receipt = "Withdrawal failed: Minimum balance violation."; return;
    }
    balance = newBal;
    receipt = "Withdrawn: " + formatMoney(amount) + " from " + accountNumber
        + " | New Balance: " + formatMoney(balance);
}

string SavingsAccount::accountType() const { return "Savings"; }

void SavingsAccount::calculateInterest(string& receipt) {
    if (status != "active") {
        receipt = "Interest calculation failed: Account frozen."; return;
    }
    double interest = roundMoney(balance * interestRate);
    balance = roundMoney(balance + interest);
    receipt = "Interest added: " + formatMoney(interest) + " | New Balance: " + formatMoney(balance);
}

CurrentAccount::CurrentAccount(string num, string date, double limit)
    : Account(num, date), overdraftLimit(limit) {}

void CurrentAccount::withdraw(double amount, string& receipt) {
    if (amount <= 0 || status != "active") {
        receipt = "Withdrawal failed: Invalid amount or account frozen."; return;
    }
    if (roundMoney(balance - amount) < -overdraftLimit) {
        receipt = "Withdrawal failed: Overdraft limit exceeded."; return;
    }
    balance = roundMoney(balance - amount);
    receipt = "Withdrawn: " + formatMoney(amount) + " from " + accountNumber
        + " | New Balance: " + formatMoney(balance);
}

string CurrentAccount::accountType() const { return "Current"; }

Transaction::Transaction(string id, string t, double amt,
    string date, string accNum, string related)
    : transactionID(id), type(t), accountNumber(accNum),
    relatedAccount(related), amount(amt), date(date) {}

string Transaction::displayReceipt() const
{
    stringstream ss;
    ss << transactionID
        << " | " << type
        << " | " << fixed << setprecision(2) << amount
        << " | " << date
        << " | " << accountNumber;
    if (!relatedAccount.empty()) ss << " -> " << relatedAccount;
    return ss.str();
}

string Transaction::getTransactionID()  const { return transactionID; }
string Transaction::getType()           const { return type; }
double Transaction::getAmount()         const { return amount; }
string Transaction::getDate()           const { return date; }
string Transaction::getAccountNumber()  const { return accountNumber; }
string Transaction::getRelatedAccount() const { return relatedAccount; }

Loan::Loan(string id, string custID, string accNum,
    double p, double r, int t)
    : loanID(id), loanCustomerID(custID), loanAccountNumber(accNum),
    principalAmount(p), interestRate(r), tenureMonths(t), status("pending") {}

double Loan::calculateEMI() const
{
    if (interestRate == 0.0) return principalAmount / tenureMonths;
    double r = interestRate / 1200.0;
    double powVal = pow(1.0 + r, tenureMonths);
    return principalAmount * r * powVal / (powVal - 1.0);
}

string Loan::displayLoanDetails() const
{
    stringstream ss;
    ss << loanID << " | Customer: " << loanCustomerID
        << " | Account: " << loanAccountNumber
        << " | Principal: " << principalAmount
        << " | Rate: " << interestRate << "%"
        << " | Tenure: " << tenureMonths << "mo"
        << " | EMI: " << calculateEMI()
        << " | Status: " << status;
    return ss.str();
}

string Loan::getLoanID()          const { return loanID; }
string Loan::getCustomerID()      const { return loanCustomerID; }
string Loan::getStatus()          const { return status; }
void   Loan::setStatus(string s) { status = s; }
double Loan::getPrincipalAmount() const { return principalAmount; }
double Loan::getInterestRate()    const { return interestRate; }
int    Loan::getTenureMonths()    const { return tenureMonths; }

Bank::Bank()
{
    dataDir = ResolveDataDirectory();

    customers = new Customer * [1000]();    customerCount = 0;
    employees = new Employee * [10]();      employeeCount = 0;
    transactions = new Transaction * [5000](); transactionCount = 0;
    loans = new Loan * [500]();         loanCount = 0;

    nextAccountNum = 2000;
    nextTransactionNum = 5000;
    nextLoanNum = 1000;
    currentSession = Session();
    dataLoaded = false;

    const string adminSaltFile = dataDir + "admin_salt.txt";
    ifstream saltIn(adminSaltFile);
    if (saltIn.is_open()) { getline(saltIn, adminPasswordSalt); saltIn.close(); }
    else {
        adminPasswordSalt = SHA256Impl::generateSalt();
        ofstream saltOut(adminSaltFile);
        if (saltOut.is_open()) { saltOut << adminPasswordSalt; saltOut.close(); }
    }
    adminPasswordHash = SHA256Impl::pbkdf2("helloboy", adminPasswordSalt);

    employees[employeeCount++] = new Employee(
        "EMP001", "Admin", 30,
        "00000-0000000-0", "000000000000", "HQ",
        "Manager", 0.0);

    loadData();
}

Bank::~Bank()
{
    saveCustomers(); saveAccounts(); saveTransactions(); saveLoans();
    for (int i = 0; i < customerCount; ++i) delete customers[i];
    for (int i = 0; i < employeeCount; ++i) delete employees[i];
    for (int i = 0; i < transactionCount; ++i) delete transactions[i];
    for (int i = 0; i < loanCount; ++i) delete loans[i];
    delete[] customers;
    delete[] employees;
    delete[] transactions;
    delete[] loans;
}

string Bank::escapeField(const string& f) { return ::escapeField(f); }
string Bank::unescapeField(const string& f) { return ::unescapeField(f); }
string Bank::getCurrentDate() { return ::getCurrentDate(); }

bool Bank::isAuthenticated(const string& requiredRole) const
{
    if (!currentSession.isLoggedIn) return false;
    if (requiredRole == "admin" && currentSession.role != "admin") return false;
    return true;
}

void Bank::initializeCounters()
{
    nextAccountNum = 2000;
    nextTransactionNum = 5000;
    nextLoanNum = 1000;

    for (int i = 0; i < customerCount; ++i) {
        if (!customers[i]) continue;
        for (int j = 0; j < customers[i]->getAccountCount(); ++j) {
            const string& num = customers[i]->getAccounts()[j]->getAccountNumber();
            if (num.rfind("ACC", 0) == 0) {
                try { int n = stoi(num.substr(3)); if (n >= nextAccountNum) nextAccountNum = n + 1; }
                catch (...) {}
            }
        }
    }
    for (int i = 0; i < transactionCount; ++i) {
        const string& tid = transactions[i]->getTransactionID();
        if (tid.rfind("TXN", 0) == 0) {
            try { int n = stoi(tid.substr(3)); if (n >= nextTransactionNum) nextTransactionNum = n + 1; }
            catch (...) {}
        }
    }
    for (int i = 0; i < loanCount; ++i) {
        const string& lid = loans[i]->getLoanID();
        if (lid.rfind("LOAN", 0) == 0) {
            try { int n = stoi(lid.substr(4)); if (n >= nextLoanNum) nextLoanNum = n + 1; }
            catch (...) {}
        }
    }
}

void Bank::saveCustomers()
{
    ofstream f(dataDir + "users.txt");
    if (!f.is_open()) return;
    for (int i = 0; i < customerCount; ++i) {
        if (!customers[i]) continue;
        f << escapeField(customers[i]->getCustomerID()) << "|"
            << escapeField(customers[i]->getName()) << "|"
            << escapeField(customers[i]->getCNIC()) << "|"
            << escapeField(customers[i]->getPhone()) << "|"
            << escapeField(customers[i]->getBackupCodeHash()) << "|"
            << escapeField(customers[i]->getBackupCodeSalt()) << "|"
            << escapeField(customers[i]->getPasswordHash()) << "|"
            << escapeField(customers[i]->getPasswordSalt()) << "\n";
    }
}

void Bank::saveTransactions()
{
    ofstream f(dataDir + "transactions.txt");
    if (!f.is_open()) return;
    for (int i = 0; i < transactionCount; ++i) {
        if (!transactions[i]) continue;
        f << escapeField(transactions[i]->getTransactionID()) << "|"
            << escapeField(transactions[i]->getType()) << "|"
            << transactions[i]->getAmount() << "|"
            << escapeField(transactions[i]->getDate()) << "|"
            << escapeField(transactions[i]->getAccountNumber()) << "|"
            << escapeField(transactions[i]->getRelatedAccount()) << "\n";
    }
}

void Bank::saveLoans()
{
    ofstream f(dataDir + "loans.txt");
    if (!f.is_open()) return;
    for (int i = 0; i < loanCount; ++i) {
        if (!loans[i]) continue;
        f << escapeField(loans[i]->getLoanID()) << "|"
            << escapeField(loans[i]->getCustomerID()) << "|"
            << escapeField(loans[i]->getAccountNumber()) << "|"
            << loans[i]->getPrincipalAmount() << "|"
            << loans[i]->getInterestRate() << "|"
            << loans[i]->getTenureMonths() << "|"
            << escapeField(loans[i]->getStatus()) << "\n";
    }
}

void Bank::saveAccounts()
{
    ofstream f(dataDir + "accounts.txt");
    if (!f.is_open()) return;
    for (int i = 0; i < customerCount; ++i) {
        if (!customers[i]) continue;
        string cid = customers[i]->getCustomerID();
        for (int j = 0; j < customers[i]->getAccountCount(); ++j) {
            Account* acc = customers[i]->getAccounts()[j];
            if (!acc) continue;
            string type = acc->accountType();
            double rate = 0, minBal = 0, limit = 0;
            if (type == "Savings") {
                auto* sa = dynamic_cast<SavingsAccount*>(acc);
                if (sa) { rate = sa->getInterestRate(); minBal = sa->getMinimumBalance(); }
            }
            else if (type == "Current") {
                auto* ca = dynamic_cast<CurrentAccount*>(acc);
                if (ca) { limit = ca->getOverdraftLimit(); }
            }
            f << cid << "|" << acc->getAccountNumber() << "|" << type << "|"
                << acc->getBalance() << "|" << acc->getStatus() << "|"
                << acc->getDateOpened() << "|"
                << rate << "|" << minBal << "|" << limit << "\n";
        }
    }
}

void Bank::loadCustomers()
{
    for (int i = 0; i < customerCount; ++i) delete customers[i];
    customerCount = 0;

    ifstream f(dataDir + "users.txt");
    if (!f.is_open()) { dataLoaded = true; return; }

    string line;
    while (getline(f, line) && customerCount < 1000) {
        if (line.empty()) continue;
        stringstream ss(line);
        string id, name, cnic, phone, bkHash, bkSalt, pwdHash, pwdSalt;
        if (!getline(ss, id, '|')) continue;
        if (!getline(ss, name, '|')) continue;
        if (!getline(ss, cnic, '|')) continue;
        if (!getline(ss, phone, '|')) continue;
        if (!getline(ss, bkHash, '|')) continue;
        if (!getline(ss, bkSalt, '|')) continue;
        if (!getline(ss, pwdHash, '|')) continue;
        if (!getline(ss, pwdSalt, '|')) continue;

        customers[customerCount++] = new Customer(
            unescapeField(id), unescapeField(name), 30,
            unescapeField(cnic), unescapeField(phone), "Address",
            unescapeField(bkHash), unescapeField(bkSalt),
            unescapeField(pwdHash), unescapeField(pwdSalt));
    }
    dataLoaded = true;
}

void Bank::loadAccounts()
{
    ifstream f(dataDir + "accounts.txt");
    if (!f.is_open()) return;
    string line;
    while (getline(f, line)) {
        if (line.empty()) continue;
        stringstream ss(line);
        string cid, accNum, type, status, date;
        double balance, rate, minBal, limit;

        if (!getline(ss, cid, '|')) continue;
        if (!getline(ss, accNum, '|')) continue;
        if (!getline(ss, type, '|')) continue;
        if (!(ss >> balance))          continue; ss.ignore();
        if (!getline(ss, status, '|')) continue;
        if (!getline(ss, date, '|')) continue;
        if (!(ss >> rate))             continue; ss.ignore();
        if (!(ss >> minBal))           continue; ss.ignore();
        if (!(ss >> limit))            continue;

        Customer* cust = nullptr;
        for (int i = 0; i < customerCount; ++i)
            if (customers[i] && customers[i]->getCustomerID() == cid)
            {
                cust = customers[i]; break;
            }
        if (!cust || cust->getAccountCount() >= 100) continue;

        Account* acc = nullptr;
        if (type == "Savings") acc = new SavingsAccount(accNum, date, rate, minBal);
        else if (type == "Current") acc = new CurrentAccount(accNum, date, limit);
        if (acc) {
            acc->setBalance(balance);
            acc->setStatusDirect(status);
            cust->addAccount(acc);
        }
    }
}

void Bank::loadTransactions()
{
    for (int i = 0; i < transactionCount; ++i) delete transactions[i];
    transactionCount = 0;

    ifstream f(dataDir + "transactions.txt");
    if (!f.is_open()) return;
    string line;
    while (getline(f, line) && transactionCount < 5000) {
        if (line.empty()) continue;
        stringstream ss(line);
        string tid, type, date, accNum, related;
        double amount;
        if (!getline(ss, tid, '|')) continue;
        if (!getline(ss, type, '|')) continue;
        if (!(ss >> amount))           continue; ss.ignore();
        if (!getline(ss, date, '|')) continue;
        if (!getline(ss, accNum, '|')) continue;
        getline(ss, related);
        transactions[transactionCount++] = new Transaction(
            unescapeField(tid), unescapeField(type), amount,
            unescapeField(date), unescapeField(accNum),
            unescapeField(related));
    }
}

void Bank::loadLoans()
{
    for (int i = 0; i < loanCount; ++i) delete loans[i];
    loanCount = 0;

    ifstream f(dataDir + "loans.txt");
    if (!f.is_open()) return;
    string line;
    while (getline(f, line) && loanCount < 500) {
        if (line.empty()) continue;
        stringstream ss(line);
        string lid, custID, accNum, status;
        double principal, rate;
        int    tenure;
        if (!getline(ss, lid, '|')) continue;
        if (!getline(ss, custID, '|')) continue;
        if (!getline(ss, accNum, '|')) continue;
        if (!(ss >> principal))        continue; ss.ignore();
        if (!(ss >> rate))             continue; ss.ignore();
        if (!(ss >> tenure))           continue; ss.ignore();
        getline(ss, status);
        auto* l = new Loan(
            unescapeField(lid), unescapeField(custID),
            unescapeField(accNum), principal, rate, tenure);
        l->setStatus(unescapeField(status));
        loans[loanCount++] = l;
    }
}

void Bank::loadData()
{
    loadCustomers();
    loadAccounts();
    loadTransactions();
    loadLoans();
    initializeCounters();
    dataLoaded = true;
}

bool Bank::login(const string& role, const string& id, const string& password)
{
    if (currentSession.lockoutUntil > 0) {
        time_t now = time(nullptr);
        if (now < currentSession.lockoutUntil) return false;
        currentSession.failedAttempts = 0;
        currentSession.lockoutUntil = 0;
    }

    bool success = false;

    if (role == "admin") {
        if (id == "EMP001") {
            string computed = SHA256Impl::pbkdf2(password, adminPasswordSalt);
            success = SHA256Impl::constantTimeEqual(computed, adminPasswordHash);
        }
    }
    else {
        if (!dataLoaded) loadData();
        for (int i = 0; i < customerCount; ++i)
            if (customers[i] && customers[i]->getCustomerID() == id)
            {
                success = customers[i]->verifyPassword(password); break;
            }
    }

    if (success) {
        currentSession.userID = id;
        currentSession.role = role;
        currentSession.isLoggedIn = true;
        currentSession.failedAttempts = 0;
        currentSession.lockoutUntil = 0;
        return true;
    }

    if (++currentSession.failedAttempts >= 5) {
        currentSession.lockoutUntil = time(nullptr) + 60;
        currentSession.failedAttempts = 0;
    }
    return false;
}

bool Bank::signup(const string& name, const string& cnic, const string& phone,
    const string& password, string& backupCode)
{
    if (!isValidDigits(cnic, 13)) return false;
    if (!isValidDigits(phone, 11)) return false;
    if (name.empty() || password.empty()) return false;
    if (password.size() < 8) return false;
    if (customerCount >= 1000) return false;
    if (!dataLoaded) loadData();

    for (int i = 0; i < customerCount; ++i)
        if (customers[i] && customers[i]->getCNIC() == cnic) return false;

    static const char alphanum[] = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    static random_device rd;
    static mt19937 gen(rd());
    uniform_int_distribution<int> dist(0, 31);

    backupCode.clear();
    for (int i = 0; i < 6; ++i) backupCode += alphanum[dist(gen)];

    string pwdSalt = SHA256Impl::generateSalt();
    string pwdHash = SHA256Impl::pbkdf2(password, pwdSalt);
    string bkSalt = SHA256Impl::generateSalt();
    string bkHash = SHA256Impl::pbkdf2(backupCode, bkSalt);

    int maxId = 100;
    for (int i = 0; i < customerCount; ++i) {
        if (!customers[i]) continue;
        const string& cid = customers[i]->getCustomerID();
        if (cid.rfind("CUST", 0) == 0) {
            try { int n = stoi(cid.substr(4)); if (n > maxId) maxId = n; }
            catch (...) {}
        }
    }

    customers[customerCount++] = new Customer(
        "CUST" + to_string(maxId + 1), name, 30, cnic, phone, "Address",
        bkHash, bkSalt, pwdHash, pwdSalt);

    saveCustomers();
    return true;
}

bool Bank::resetPassword(const string& customerID, const string& cnic,
    const string& backupCode, const string& newPassword, string& newBackupCode)
{
    if (!dataLoaded) loadData();
    if (newPassword.size() < 8) return false;

    for (int i = 0; i < customerCount; ++i) {
        if (!customers[i]) continue;
        if (customers[i]->getCustomerID() == customerID &&
            customers[i]->getCNIC() == cnic &&
            customers[i]->verifyBackupCode(backupCode))
        {
            string newPwdSalt = SHA256Impl::generateSalt();
            customers[i]->setPasswordHash(
                SHA256Impl::pbkdf2(newPassword, newPwdSalt));
            customers[i]->setPasswordSalt(newPwdSalt);

            static const char alphanum[] = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            static random_device rd;
            static mt19937 gen(rd());
            uniform_int_distribution<int> dist(0, 31);

            newBackupCode.clear();
            for (int j = 0; j < 6; ++j)
                newBackupCode += alphanum[dist(gen)];

            string newBkSalt = SHA256Impl::generateSalt();
            string newBkHash = SHA256Impl::pbkdf2(newBackupCode, newBkSalt);
            customers[i]->setBackupCodeHash(newBkHash);
            customers[i]->setBackupCodeSalt(newBkSalt);

            saveCustomers();
            return true;
        }
    }
    return false;
}

void Bank::logout() { currentSession = Session(); }

string Bank::getAccountBalance(const string& accNum)
{
    if (!dataLoaded) loadData();

    if (currentSession.role == "customer") {
        bool owns = false;
        for (int i = 0; i < customerCount && !owns; ++i)
            if (customers[i] && customers[i]->getCustomerID() == currentSession.userID)
                for (int j = 0; j < customers[i]->getAccountCount() && !owns; ++j)
                    if (customers[i]->getAccounts()[j] &&
                        customers[i]->getAccounts()[j]->getAccountNumber() == accNum)
                        owns = true;
        if (!owns) return "Error: Access denied. You do not own this account.";
    }

    for (int i = 0; i < customerCount; ++i)
        for (int j = 0; j < customers[i]->getAccountCount(); ++j) {
            Account* acc = customers[i]->getAccounts()[j];
            if (acc && acc->getAccountNumber() == accNum) {
                stringstream ss;
                ss << "Account: " << accNum << "\nBalance: $" << formatMoney(acc->getBalance())
                    << "\nStatus: " << (acc->getStatus() == "frozen" ? "FROZEN" : "Active")
                    << "\nType: " << acc->accountType();
                return ss.str();
            }
        }
    return "Error: Account not found: " + accNum;
}

string Bank::listCustomerAccounts(const string& customerId)
{
    if (!dataLoaded) loadData();
    for (int i = 0; i < customerCount; ++i) {
        if (!customers[i] || customers[i]->getCustomerID() != customerId) continue;
        int cnt = customers[i]->getAccountCount();
        if (cnt == 0) return "No accounts found for this customer.";
        stringstream ss;
        for (int j = 0; j < cnt; ++j)
            ss << customers[i]->getAccounts()[j]->displayStatement() << "\n";
        return ss.str();
    }
    return "Error: Customer not found.";
}

string Bank::openAccount(const string& customerID, const string& type,
    double rateOrLimit, double minBal)
{
    if (!isAuthenticated("admin") &&
        !(isAuthenticated("customer") && currentSession.userID == customerID))
        return "Error: Authentication required.";
    if (!dataLoaded) loadData();

    Customer* c = nullptr;
    for (int i = 0; i < customerCount; ++i)
        if (customers[i] && customers[i]->getCustomerID() == customerID)
        {
            c = customers[i]; break;
        }
    if (!c) return "Error: Customer not found.";
    if (c->getAccountCount() >= 100) return "Error: Account limit reached.";

    string accNum = "ACC" + to_string(nextAccountNum++);
    string date = getCurrentDate();

    if (type == "savings") c->addAccount(new SavingsAccount(accNum, date, rateOrLimit, minBal));
    else if (type == "current") c->addAccount(new CurrentAccount(accNum, date, rateOrLimit));
    else return "Error: Invalid account type.";

    saveAccounts();
    return "Success: Account opened: " + accNum;
}

string Bank::freezeAccount(const string& accNum)
{
    if (!isAuthenticated("admin")) return "Error: Admin authentication required.";
    if (!dataLoaded) loadData();
    for (int i = 0; i < customerCount; ++i)
        for (int j = 0; j < customers[i]->getAccountCount(); ++j) {
            Account* acc = customers[i]->getAccounts()[j];
            if (acc && acc->getAccountNumber() == accNum) {
                acc->setStatus("frozen");
                saveAccounts();
                return "Success: Account frozen: " + accNum;
            }
        }
    return "Error: Account not found.";
}

string Bank::unfreezeAccount(const string& accNum)
{
    if (!isAuthenticated("admin")) return "Error: Admin authentication required.";
    if (!dataLoaded) loadData();
    for (int i = 0; i < customerCount; ++i)
        for (int j = 0; j < customers[i]->getAccountCount(); ++j) {
            Account* acc = customers[i]->getAccounts()[j];
            if (acc && acc->getAccountNumber() == accNum) {
                acc->setStatus("active");
                saveAccounts();
                return "Success: Account unfrozen: " + accNum;
            }
        }
    return "Error: Account not found.";
}

string Bank::deposit(const string& accNum, double amount)
{
    if (!isAuthenticated("admin") && !isAuthenticated("customer"))
        return "Error: Authentication required.";
    if (amount <= 0) return "Error: Invalid amount.";
    if (!dataLoaded) loadData();

    if (currentSession.role == "customer") {
        bool owns = false;
        for (int i = 0; i < customerCount && !owns; ++i)
            if (customers[i] && customers[i]->getCustomerID() == currentSession.userID)
                for (int j = 0; j < customers[i]->getAccountCount() && !owns; ++j)
                    if (customers[i]->getAccounts()[j] &&
                        customers[i]->getAccounts()[j]->getAccountNumber() == accNum)
                        owns = true;
        if (!owns) return "Error: Access denied.";
    }

    for (int i = 0; i < customerCount; ++i)
        for (int j = 0; j < customers[i]->getAccountCount(); ++j) {
            Account* acc = customers[i]->getAccounts()[j];
            if (acc && acc->getAccountNumber() == accNum) {
                string receipt;
                acc->deposit(amount, receipt);
                if (receipt.find("failed") != string::npos) return "Error: " + receipt;
                transactions[transactionCount++] = new Transaction(
                    "TXN" + to_string(nextTransactionNum++),
                    "Deposit", amount, getCurrentDate(), accNum);
                saveTransactions(); saveAccounts();
                return "Success: " + receipt;
            }
        }
    return "Error: Account not found.";
}

string Bank::withdraw(const string& accNum, double amount)
{
    if (!isAuthenticated("admin") && !isAuthenticated("customer"))
        return "Error: Authentication required.";
    if (amount <= 0) return "Error: Invalid amount.";
    if (!dataLoaded) loadData();

    if (currentSession.role == "customer") {
        bool owns = false;
        for (int i = 0; i < customerCount && !owns; ++i)
            if (customers[i] && customers[i]->getCustomerID() == currentSession.userID)
                for (int j = 0; j < customers[i]->getAccountCount() && !owns; ++j)
                    if (customers[i]->getAccounts()[j] &&
                        customers[i]->getAccounts()[j]->getAccountNumber() == accNum)
                        owns = true;
        if (!owns) return "Error: Access denied.";
    }

    for (int i = 0; i < customerCount; ++i)
        for (int j = 0; j < customers[i]->getAccountCount(); ++j) {
            Account* acc = customers[i]->getAccounts()[j];
            if (acc && acc->getAccountNumber() == accNum) {
                if (acc->getStatus() == "frozen") return "Error: Account frozen.";
                string receipt;
                acc->withdraw(amount, receipt);
                if (receipt.find("failed") != string::npos) return "Error: " + receipt;
                transactions[transactionCount++] = new Transaction(
                    "TXN" + to_string(nextTransactionNum++),
                    "Withdraw", amount, getCurrentDate(), accNum);
                saveTransactions(); saveAccounts();
                return "Success: " + receipt;
            }
        }
    return "Error: Account not found.";
}

string Bank::transfer(const string& fromAcc, const string& toAcc, double amount)
{
    if (!isAuthenticated("admin") && !isAuthenticated("customer"))
        return "Error: Authentication required.";
    if (amount <= 0)      return "Error: Invalid amount.";
    if (fromAcc == toAcc) return "Error: Cannot transfer to same account.";
    if (!dataLoaded) loadData();

    if (currentSession.role == "customer") {
        bool owns = false;
        for (int i = 0; i < customerCount && !owns; ++i)
            if (customers[i] && customers[i]->getCustomerID() == currentSession.userID)
                for (int j = 0; j < customers[i]->getAccountCount() && !owns; ++j)
                    if (customers[i]->getAccounts()[j] &&
                        customers[i]->getAccounts()[j]->getAccountNumber() == fromAcc)
                        owns = true;
        if (!owns) return "Error: Access denied. You do not own the source account.";
    }

    Account* from = nullptr, * to = nullptr;
    for (int i = 0; i < customerCount; ++i)
        for (int j = 0; j < customers[i]->getAccountCount(); ++j) {
            Account* acc = customers[i]->getAccounts()[j];
            if (!acc) continue;
            if (acc->getAccountNumber() == fromAcc) from = acc;
            if (acc->getAccountNumber() == toAcc)   to = acc;
        }

    if (!from || !to) return "Error: One or both accounts not found.";
    if (from->getStatus() == "frozen" || to->getStatus() == "frozen")
        return "Error: Account frozen.";

    string receipt;
    from->withdraw(amount, receipt);
    if (receipt.find("failed") != string::npos) return "Error: " + receipt;

    to->deposit(amount, receipt);
    if (receipt.find("failed") != string::npos) {
        string rollback;
        from->deposit(amount, rollback);
        return "Error: Transfer failed at destination.";
    }

    transactions[transactionCount++] = new Transaction(
        "TXN" + to_string(nextTransactionNum++),
        "Transfer", amount, getCurrentDate(), fromAcc, toAcc);
    saveTransactions(); saveAccounts();
    return "Success: Transfer completed. " + formatMoney(amount)
        + " from " + fromAcc + " to " + toAcc;
}

string Bank::getAccountTransactions(const string& accNum)
{
    if (!isAuthenticated("admin") && !isAuthenticated("customer"))
        return "Error: Authentication required.";
    if (!dataLoaded) loadData();

    if (currentSession.role == "customer") {
        bool owns = false;
        for (int i = 0; i < customerCount && !owns; ++i)
            if (customers[i] && customers[i]->getCustomerID() == currentSession.userID)
                for (int j = 0; j < customers[i]->getAccountCount() && !owns; ++j)
                    if (customers[i]->getAccounts()[j] &&
                        customers[i]->getAccounts()[j]->getAccountNumber() == accNum)
                        owns = true;
        if (!owns) return "Error: Access denied.";
    }

    stringstream ss;
    bool found = false;
    for (int i = 0; i < transactionCount; ++i)
        if (transactions[i] &&
            (transactions[i]->getAccountNumber() == accNum ||
                transactions[i]->getRelatedAccount() == accNum))
        {
            found = true; ss << transactions[i]->displayReceipt() << "\n";
        }

    return found ? ss.str() : "No transactions for this account.";
}

string Bank::applyLoan(const string& customerID, const string& accountNumber,
    double principal, double rate, int tenure)
{
    if (!isAuthenticated("customer") || currentSession.userID != customerID)
        return "Error: Customer authentication required.";
    if (principal <= 0 || rate < 0 || tenure <= 0)
        return "Error: Invalid loan parameters.";
    if (loanCount >= 500) return "Error: Loan limit reached.";

    Customer* c = nullptr;
    for (int i = 0; i < customerCount; ++i)
        if (customers[i] && customers[i]->getCustomerID() == customerID)
        {
            c = customers[i]; break;
        }

    bool accountFound = false;
    if (c)
        for (int j = 0; j < c->getAccountCount() && !accountFound; ++j)
            if (c->getAccounts()[j] &&
                c->getAccounts()[j]->getAccountNumber() == accountNumber)
                accountFound = true;
    if (!accountFound) return "Error: Account not found or does not belong to you.";

    string loanID = "LOAN" + to_string(nextLoanNum++);
    auto* loan = new Loan(loanID, customerID, accountNumber, principal, rate, tenure);
    loans[loanCount++] = loan;
    saveLoans();

    return "Success: Loan applied: " + loanID
        + " | Disbursed to: " + accountNumber
        + " | EMI: " + to_string(loan->calculateEMI());
}

string Bank::approveLoan(const string& loanID)
{
    if (!isAuthenticated("admin")) return "Error: Admin authentication required.";
    if (!dataLoaded) loadData();

    for (int i = 0; i < loanCount; ++i) {
        if (!loans[i] || loans[i]->getLoanID() != loanID) continue;
        if (loans[i]->getStatus() != "pending") return "Error: Loan already processed.";

        string custID = loans[i]->getCustomerID();
        string accNum = loans[i]->getAccountNumber();
        double amount = loans[i]->getPrincipalAmount();

        for (int j = 0; j < customerCount; ++j) {
            if (!customers[j] || customers[j]->getCustomerID() != custID) continue;
            for (int k = 0; k < customers[j]->getAccountCount(); ++k) {
                Account* acc = customers[j]->getAccounts()[k];
                if (!acc || acc->getAccountNumber() != accNum) continue;
                string receipt;
                acc->deposit(amount, receipt);
                if (receipt.find("failed") != string::npos) {
                    loans[i]->setStatus("failed");
                    saveLoans();
                    return "Error: Deposit failed: " + receipt;
                }
                loans[i]->setStatus("approved");
                saveLoans(); saveAccounts();
                return "Success: Loan " + loanID + " APPROVED. $"
                    + formatMoney(amount) + " deposited to " + accNum;
            }
        }
        loans[i]->setStatus("failed");
        saveLoans();
        return "Error: Account not found for disbursement.";
    }
    return "Error: Loan not found.";
}

string Bank::rejectLoan(const string& loanID)
{
    if (!isAuthenticated("admin")) return "Error: Admin authentication required.";
    if (!dataLoaded) loadData();
    for (int i = 0; i < loanCount; ++i) {
        if (!loans[i] || loans[i]->getLoanID() != loanID) continue;
        if (loans[i]->getStatus() != "pending") return "Error: Loan already processed.";
        loans[i]->setStatus("rejected");
        saveLoans();
        return "Loan " + loanID + " REJECTED. No amount disbursed.";
    }
    return "Error: Loan not found.";
}

string Bank::getAllPendingLoans()
{
    if (!isAuthenticated("admin")) return "Error: Admin authentication required.";
    if (!dataLoaded) loadData();

    stringstream ss;
    bool found = false;
    for (int i = 0; i < loanCount; ++i) {
        if (!loans[i] || loans[i]->getStatus() != "pending") continue;
        found = true;
        string custName = "Unknown", custCNIC = "Unknown";
        for (int j = 0; j < customerCount; ++j)
            if (customers[j] && customers[j]->getCustomerID() == loans[i]->getCustomerID())
            {
                custName = customers[j]->getName(); custCNIC = customers[j]->getCNIC(); break;
            }

        ss << "\n========================================\n"
            << "Loan Application #" << (i + 1) << "\n"
            << "========================================\n"
            << "Loan ID: " << loans[i]->getLoanID() << "\n"
            << "Customer: " << custName << "\n"
            << "Customer ID: " << loans[i]->getCustomerID() << "\n"
            << "CNIC: " << custCNIC << "\n"
            << "Account: " << loans[i]->getAccountNumber() << "\n"
            << "Principal: $" << loans[i]->getPrincipalAmount() << "\n"
            << "Interest Rate: " << loans[i]->getInterestRate() << "%\n"
            << "Tenure: " << loans[i]->getTenureMonths() << " months\n"
            << "Monthly EMI: $" << loans[i]->calculateEMI() << "\n"
            << "Status: " << "PENDING" << "\n"
            << "========================================\n";
    }
    return found ? ss.str() : "No pending loan applications.";
}

string Bank::getMyLoans()
{
    if (!isAuthenticated("customer"))
        return "Error: Customer authentication required.";
    if (!dataLoaded) loadData();

    const string& custID = currentSession.userID;
    stringstream ss;
    bool found = false;

    for (int i = 0; i < loanCount; ++i) {
        if (!loans[i]) continue;
        if (loans[i]->getCustomerID() != custID) continue;

        found = true;

        double emi = loans[i]->calculateEMI();
        stringstream emiSS;
        emiSS.precision(2);
        emiSS << fixed << emi;

        ss << loans[i]->getLoanID() << " | "
            << loans[i]->getAccountNumber() << " | "
            << loans[i]->getPrincipalAmount() << " | "
            << loans[i]->getInterestRate() << " | "
            << loans[i]->getTenureMonths() << " | "
            << emiSS.str() << " | "
            << loans[i]->getStatus() << "\n";
    }

    return found ? ss.str() : "No loan applications found.";
}

string Bank::addCustomer(const string& name, int age, const string& cnic,
    const string& phone, const string& address)
{
    if (!isAuthenticated("admin")) return "Error: Admin authentication required.";
    if (!isValidDigits(cnic, 13)) return "Error: Invalid CNIC.";
    if (!isValidDigits(phone, 11)) return "Error: Invalid Phone.";
    if (name.empty())              return "Error: Invalid input.";
    for (int i = 0; i < customerCount; ++i)
        if (customers[i] && customers[i]->getCNIC() == cnic)
            return "Error: CNIC already registered.";
    if (customerCount >= 1000) return "Error: Customer limit reached.";

    int maxId = 100;
    for (int i = 0; i < customerCount; ++i) {
        if (!customers[i]) continue;
        const string& cid = customers[i]->getCustomerID();
        if (cid.rfind("CUST", 0) == 0) {
            try { int n = stoi(cid.substr(4)); if (n > maxId) maxId = n; }
            catch (...) {}
        }
    }
    string salt = SHA256Impl::generateSalt();
    string hash = SHA256Impl::pbkdf2("", salt);
    string newID = "CUST" + to_string(maxId + 1);

    customers[customerCount++] = new Customer(
        newID, name, age, cnic, phone, address,
        hash, salt, hash, salt);
    saveCustomers();
    return "Success: Customer added: " + newID;
}

string Bank::searchCustomer(const string& query)
{
    if (!isAuthenticated("admin")) return "Error: Admin authentication required.";
    if (!dataLoaded) loadData();
    stringstream ss;
    bool found = false;
    for (int i = 0; i < customerCount; ++i) {
        if (!customers[i]) continue;
        bool match = customers[i]->getName().find(query) != string::npos
            || customers[i]->getCNIC() == query;
        if (!match) continue;
        found = true;
        ss << customers[i]->displayInfo() << "\n";
    }
    return found ? ss.str() : "No match found.";
}

string Bank::displayAllCustomers()
{
    if (!isAuthenticated("admin")) return "Error: Admin authentication required.";
    if (!dataLoaded) loadData();
    if (customerCount == 0) return "No customers.";
    stringstream ss;
    for (int i = 0; i < customerCount; ++i) {
        if (!customers[i]) continue;
        ss << customers[i]->displayInfo() << "\n";
    }
    return ss.str();
}

extern "C" {

    __declspec(dllexport) Bank* __stdcall CreateBank() { return new Bank(); }
    __declspec(dllexport) void  __stdcall DeleteBank(Bank* b) { delete b; }

    __declspec(dllexport) const char* __stdcall ExecuteCommand(
        Bank* b, const char* cmd,
        const char* p1, const char* p2, const char* p3, const char* p4)
    {
        thread_local static string result;
        try {
            string command(cmd ? cmd : "");
            string a1(p1 ? p1 : ""), a2(p2 ? p2 : ""), a3(p3 ? p3 : ""), a4(p4 ? p4 : "");

            for (auto& ch : command)
                ch = static_cast<char>(tolower(static_cast<unsigned char>(ch)));

            if (command == "signup") {
                string backup;
                if (b->signup(a1, a2, a3, a4, backup)) {
                    int maxId = 100;
                    Customer** custs = b->getCustomers();
                    int count = b->getCustomerCount();
                    for (int i = 0; i < count; ++i) {
                        if (!custs[i]) continue;
                        const string& cid = custs[i]->getCustomerID();
                        if (cid.rfind("CUST", 0) == 0) {
                            try { int n = stoi(cid.substr(4)); if (n > maxId) maxId = n; }
                            catch (...) {}
                        }
                    }
                    result = "Success: Signup completed.\n"
                        "Your Customer ID: CUST" + to_string(maxId) + "\n"
                        "Backup Code: " + backup + "\n"
                        "IMPORTANT: Save this backup code securely. "
                        "It is shown only once and required for password recovery.";
                }
                else {
                    result = "Error: Signup failed. CNIC may already be registered, "
                        "password too short, or input invalid.";
                }
            }

            else if (command == "login") {
                string role = a3.empty() ? "customer" : a3;
                const Session& sess = b->getSession();
                if (sess.lockoutUntil > 0) {
                    time_t now = time(nullptr);
                    if (now < sess.lockoutUntil) {
                        long secs = static_cast<long>(sess.lockoutUntil - now);
                        result = "Error: Too many failed attempts. "
                            "Please wait " + to_string(secs) + " seconds.";
                        return result.c_str();
                    }
                }
                if (b->login(role, a1, a2)) {
                    if (role == "admin") {
                        result = "Success: Administrator";
                    }
                    else {
                        string custName = "Customer";
                        Customer** custs = b->getCustomers();
                        int count = b->getCustomerCount();
                        for (int i = 0; i < count; ++i)
                            if (custs[i] && custs[i]->getCustomerID() == a1)
                            {
                                custName = custs[i]->getName(); break;
                            }
                        result = "Success: " + custName;
                    }
                }
                else {
                    result = "Error: Invalid credentials.";
                }
            }

            else if (command == "reset_password") {
                if (a4.size() < 8) {
                    result = "Error: New password must be at least 8 characters.";
                }
                else {
                    string newBackupCode;
                    if (b->resetPassword(a1, a2, a3, a4, newBackupCode)) {
                        result = "Success: Password reset completed. New backup code: " + newBackupCode;
                    }
                    else {
                        result = "Error: Invalid details. Check Customer ID, CNIC, and Backup Code.";
                    }
                }
            }

            else if (command == "add_customer") {
                if (a2.empty() || a4.empty()) result = "Error: Missing required fields.";
                else {
                    try { result = b->addCustomer(a1, stoi(a2), a3, a4, "HQ"); }
                    catch (...) { result = "Error: Invalid age value."; }
                }
            }

            else if (command == "open_account") {
                try {
                    double minBal = a4.empty() ? 0.0 : stod(a4);
                    result = b->openAccount(a1, a2, stod(a3), minBal);
                }
                catch (...) { result = "Error: Invalid numeric parameters."; }
            }

            else if (command == "deposit") {
                try { result = b->deposit(a1, a2.empty() ? 0.0 : stod(a2)); }
                catch (...) { result = "Error: Invalid amount."; }
            }

            else if (command == "withdraw") {
                try { result = b->withdraw(a1, a2.empty() ? 0.0 : stod(a2)); }
                catch (...) { result = "Error: Invalid amount."; }
            }

            else if (command == "transfer") {
                try { result = b->transfer(a1, a2, a3.empty() ? 0.0 : stod(a3)); }
                catch (...) { result = "Error: Invalid amount."; }
            }

            else if (command == "apply_loan") {
                if (a1.empty() || a2.empty() || a3.empty() || a4.empty()) {
                    result = "Error: Missing loan parameters.";
                }
                else {
                    try {
                        size_t pos = a1.find('|');
                        string custId = a1.substr(0, pos);
                        string accNum = (pos != string::npos) ? a1.substr(pos + 1) : "";
                        if (accNum.empty()) result = "Error: Account number required.";
                        else result = b->applyLoan(custId, accNum, stod(a2), stod(a3), stoi(a4));
                    }
                    catch (...) { result = "Error: Invalid loan parameters."; }
                }
            }

            else if (command == "my_loans") {
                result = b->getMyLoans();
            }

            else if (command == "get_balance") {
                result = a1.empty() ? "Error: Account number required."
                    : b->getAccountBalance(a1);
            }

            else if (command == "list_accounts") {
                result = a1.empty() ? "Error: Customer ID required."
                    : b->listCustomerAccounts(a1);
            }

            else if (command == "pending_loans") { result = b->getAllPendingLoans(); }

            else if (command == "approve_loan") { result = b->approveLoan(a1); }

            else if (command == "reject_loan") { result = b->rejectLoan(a1); }

            else if (command == "freeze") { result = b->freezeAccount(a1); }
            else if (command == "unfreeze") { result = b->unfreezeAccount(a1); }

            else if (command == "search") { result = b->searchCustomer(a1); }
            else if (command == "list") { result = b->displayAllCustomers(); }

            else if (command == "history") { result = b->getAccountTransactions(a1); }

            else if (command == "logout") { b->logout(); result = "Success: Logged out."; }

            else {
                result = "Warning: Unknown command. Available: signup, login, reset_password, "
                    "add_customer, open_account, deposit, withdraw, transfer, apply_loan, "
                    "my_loans, approve_loan, reject_loan, get_balance, list_accounts, "
                    "pending_loans, freeze, unfreeze, search, list, history, logout";
            }
        }
        catch (const exception& e) { result = string("Error: ") + e.what(); }
        catch (...) { result = "Error: Unknown error occurred."; }

        return result.c_str();
    }

}