using WebAPI.Data.Entities;
using WebAPI.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using WebAPI.Models;
namespace WebAPI.Services
{
    public class AccountingService
    {
        private readonly ProjectDbContextFactory _factory;

        public AccountingService(ProjectDbContextFactory factory)
        {
            _factory = factory;
        }

        // Create journal entry + lines (unposted)
        public async Task<int> CreateJournalEntryAsync(string projectSchema, JournalEntry entry, IEnumerable<JournalLine> lines)
        {
            using var db = _factory.Create(projectSchema);
            entry.CreatedAt = DateTime.UtcNow;
            await db.JournalEntries.AddAsync(entry);
            await db.SaveChangesAsync();

            foreach (var l in lines)
            {
                l.JournalEntryId = entry.Id;
                await db.JournalLines.AddAsync(l);
            }

            await db.SaveChangesAsync();
            return entry.Id;
        }


        public async Task UpdateJournalEntryAsync(string projectSchema, int journalEntryId, JournalEntry updatedEntry, IEnumerable<JournalLine> updatedLines)
        {
            using var db = _factory.Create(projectSchema);
            using var tx = await db.Database.BeginTransactionAsync();

            var entry = await db.JournalEntries
              //  .Include(e => e.Lines)
                .FirstOrDefaultAsync(e => e.Id == journalEntryId);

            if (entry == null)
                throw new InvalidOperationException("Journal entry not found.");

            if (entry.Posted)
                throw new InvalidOperationException("Cannot edit a posted journal entry.");

            // ✅ تحديث بيانات الرأس (Header)
            entry.Date = updatedEntry.Date;
            entry.Description = updatedEntry.Description;
            entry.EntryNumber = updatedEntry.EntryNumber;

            // ✅ حذف الخطوط القديمة
            var oldLines = await db.JournalLines.Where(l => l.JournalEntryId == journalEntryId).ToListAsync();
            db.JournalLines.RemoveRange(oldLines);

            // ✅ إضافة الخطوط الجديدة
            foreach (var l in updatedLines)
            {
                l.JournalEntryId = entry.Id;
                await db.JournalLines.AddAsync(l);
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }


        public async Task DeleteJournalEntryAsync(string projectSchema, int journalEntryId)
        {
            using var db = _factory.Create(projectSchema);
            using var tx = await db.Database.BeginTransactionAsync();

            var entry = await db.JournalEntries
               // .Include(e => e.Lines)
                .FirstOrDefaultAsync(e => e.Id == journalEntryId);

            if (entry == null)
                throw new InvalidOperationException("Journal entry not found.");

            if (entry.Posted)
                throw new InvalidOperationException("Cannot delete a posted journal entry.");

            // ✅ حذف الخطوط المرتبطة أولاً
            IEnumerable<JournalLine> lines = db.JournalLines.Where(j=>j.JournalEntryId==entry.Id);
            db.JournalLines.RemoveRange(lines);
            // ✅ ثم حذف القيد نفسه
            db.JournalEntries.Remove(entry);

            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }

        // Post an entry: move journal lines into ledger and update balances
        public async Task PostJournalEntryAsync(string projectSchema, int journalEntryId)
        {
            using var db = _factory.Create(projectSchema);
            using var tx = await db.Database.BeginTransactionAsync();

            var entry = await db.JournalEntries.FindAsync(journalEntryId);
            if (entry == null) throw new InvalidOperationException("Journal entry not found");
            if (entry.Posted) return;

            var lines = await db.JournalLines.Where(l => l.JournalEntryId == journalEntryId).ToListAsync();

            foreach (var line in lines)
            {
                var chart = await db.ChartOfAccounts.FindAsync(line.AccountId);
                if (chart == null)
                    throw new Exception($"ChartOfAccount missing for account ID {line.AccountId}");

                var account = await db.Accounts.FirstOrDefaultAsync(a => a.AccountId == line.AccountId);
                if (account == null)
                    throw new Exception($"Account table entry missing for account ID {line.AccountId}");

                decimal change = 0;

                switch (chart.AccountType)
                {
                    case "Asset":
                    case "Expense":
                        change = line.Debit - line.Credit;
                        break;

                    case "Liability":
                    case "Equity":
                    case "Revenue":
                        change = line.Credit - line.Debit;
                        break;
                }

                account.Balance += change;

                // Ledger row
                var lastBalance = await db.LedgerEntries
                    .Where(l => l.AccountId == line.AccountId)
                    .OrderByDescending(l => l.Id)
                    .Select(l => l.Balance)
                    .FirstOrDefaultAsync();

                var ledger = new LedgerEntry
                {
                    AccountId = line.AccountId,
                    Date = entry.Date,
                    JournalEntryId = entry.Id,
                    Description = line.Description ?? entry.Description,
                    Debit = line.Debit,
                    Credit = line.Credit,
                    Balance = lastBalance + (line.Debit - line.Credit)
                };

                await db.LedgerEntries.AddAsync(ledger);
            }

            entry.Posted = true;
            await db.SaveChangesAsync();

            await tx.CommitAsync();
        }




        public async Task UnpostJournalEntryAsync(string projectSchema, int journalEntryId)
        {
            using var db = _factory.Create(projectSchema);
            using var tx = await db.Database.BeginTransactionAsync();

            var entry = await db.JournalEntries.FindAsync(journalEntryId);
            if (entry == null) throw new InvalidOperationException("Entry not found");
            if (!entry.Posted) throw new InvalidOperationException("Not posted");

            var lines = await db.JournalLines.Where(l => l.JournalEntryId == journalEntryId).ToListAsync();

            foreach (var line in lines)
            {
                var chart = await db.ChartOfAccounts.FindAsync(line.AccountId);
                var account = await db.Accounts.FirstOrDefaultAsync(a => a.AccountId == line.AccountId);

                decimal change = 0;

                switch (chart.AccountType)
                {
                    case "Asset":
                    case "Expense":
                        change = -(line.Debit - line.Credit);
                        break;

                    case "Liability":
                    case "Equity":
                    case "Revenue":
                        change = -(line.Credit - line.Debit);
                        break;
                }

                account.Balance += change;
            }

            db.LedgerEntries.RemoveRange(
                db.LedgerEntries.Where(l => l.JournalEntryId == journalEntryId)
            );

            entry.Posted = false;

            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }




        // Trial Balance: sum debits & credits per account
        public async Task<IEnumerable<object>> GetTrialBalance(string projectSchema)
        {
            using var db = _factory.Create(projectSchema);

            var qb = from coa in db.ChartOfAccounts
                     join led in db.LedgerEntries on coa.Id equals led.AccountId into ledg
                     from l in ledg.DefaultIfEmpty()
                     group l by new { coa.Id, coa.AccountCode, coa.AccountName, coa.AccountType } into g
                     select new
                     {
                         AccountId = g.Key.Id,
                         g.Key.AccountCode,
                         g.Key.AccountName,
                         g.Key.AccountType,
                         Debit = g.Sum(x => x == null ? 0 : x.Debit),
                         Credit = g.Sum(x => x == null ? 0 : x.Credit),
                         Balance = g.Sum(x => x == null ? 0 : (x.Debit - x.Credit))
                     };

            return await qb.ToListAsync<object>();
        }

        // Income statement (revenues - expenses)
        public async Task<object> GetIncomeStatement(string projectSchema, DateTime? fromDate = null, DateTime? toDate = null)
        {
            using var db = _factory.Create(projectSchema);

            var query =
                from l in db.LedgerEntries
                join c in db.ChartOfAccounts on l.AccountId equals c.Id
                where (c.AccountType == "Revenue" || c.AccountType == "Expense")
                      && (!fromDate.HasValue || l.Date >= fromDate.Value)
                      && (!toDate.HasValue || l.Date <= toDate.Value)
                group l by c.AccountType into g
                select new
                {
                    Type = g.Key,
                    Debit = g.Sum(x => x.Debit),
                    Credit = g.Sum(x => x.Credit)
                };

            var list = (await query.ToListAsync())
                .Select(x => new
                {
                    x.Type,
                    Amount = x.Type == "Revenue"
                        ? x.Credit - x.Debit
                        : x.Debit - x.Credit
                })
                .ToList();

            var totalRev = list.Where(x => x.Type == "Revenue").Sum(x => x.Amount);
            var totalExp = list.Where(x => x.Type == "Expense").Sum(x => x.Amount);

            return new
            {
                TotalRevenue = totalRev,
                TotalExpense = totalExp,
                NetProfit = totalRev - totalExp
            };
        }



        // Balance sheet: aggregate ledger by account types
        public async Task<object> GetBalanceSheet(string projectSchema)
        {
            using var db = _factory.Create(projectSchema);

            var query = from l in db.LedgerEntries
                        join c in db.ChartOfAccounts on l.AccountId equals c.Id
                        group l by c.AccountType into g
                        select new
                        {
                            Type = g.Key,
                            Debit = g.Sum(x => x.Debit),
                            Credit = g.Sum(x => x.Credit)
                        };

            var list = (await query.ToListAsync())
                .Select(x => new
                {
                    x.Type,
                    Balance = x.Type == "Asset" || x.Type == "Expense"
                              ? x.Debit - x.Credit
                              : x.Credit - x.Debit
                })
                .ToList();

            var assets = list.FirstOrDefault(x => x.Type == "Asset")?.Balance ?? 0;
            var liabilities = list.FirstOrDefault(x => x.Type == "Liability")?.Balance ?? 0;
            var equity = list.FirstOrDefault(x => x.Type == "Equity")?.Balance ?? 0;

            return new
            {
                Assets = assets,
                Liabilities = liabilities,
                Equity = equity,
                IsBalanced = Math.Round(assets, 2) == Math.Round(liabilities + equity, 2)
            };
        }


        // Ledger for account
        public async Task<IEnumerable<LedgerEntry>> GetLedgerForAccount(string projectSchema, int accountId)
        {
            using var db = _factory.Create(projectSchema);
            return await db.LedgerEntries.Where(l => l.AccountId == accountId).OrderBy(l => l.Date).ThenBy(l => l.Id).ToListAsync();
        }
        // Ledger for project
        public async Task<IEnumerable<General_Ledger>> GetLedgerForProject(string projectSchema)
        {
            using var db = _factory.Create(projectSchema);

            var result =
                await (from d in db.LedgerEntries
                       join c in db.ChartOfAccounts
                            on d.AccountId equals c.Id
                       join j in db.JournalEntries
                            on d.JournalEntryId equals j.Id
                       orderby d.Date, d.Id
                       select new General_Ledger
                       {
                           AccountName = c.AccountName,
                           AccountType = c.AccountType,
                           Description = j.Description ?? "",
                           Debit = d.Debit,
                           Credit = d.Credit,
                           Balance = d.Balance,
                           Date = d.Date
                       }).ToListAsync();

            return result;
        }
        public async Task<IEnumerable<object>> GetCashFlowForAllAccounts(string projectSchema, DateTime? from = null, DateTime? to = null)
        {
            using var db = _factory.Create(projectSchema);

            var ledger = db.LedgerEntries.AsQueryable();

            if (from.HasValue)
                ledger = ledger.Where(x => x.Date >= from.Value);

            if (to.HasValue)
                ledger = ledger.Where(x => x.Date <= to.Value);

            var query =
                from l in ledger
                join c in db.ChartOfAccounts on l.AccountId equals c.Id
                group l by new { c.Id, c.AccountName, c.AccountType } into g
                select new
                {
                    AccountId = g.Key.Id,
                    g.Key.AccountName,
                    g.Key.AccountType,
                    Inflow = g.Sum(x => x.Debit),
                    Outflow = g.Sum(x => x.Credit),
                    NetCashFlow = g.Sum(x => x.Debit - x.Credit)
                };

            return await query.ToListAsync<object>();
        }

        public async Task<decimal> GetAvailableCash(string projectSchema)
        {
            using var db = _factory.Create(projectSchema);

            var cashAccounts = await db.ChartOfAccounts
                .Where(a =>
                    a.AccountName.Contains("نقد") ||
                    a.AccountName.Contains("Cash") ||
                    a.AccountName.Contains("بنك") ||
                    a.AccountName.Contains("Bank") || a.AccountName.Contains("النقدية") || a.AccountName.Contains("حساب بنكي") || a.AccountName.Contains("البنك") || a.AccountName.Contains("الحساب البنكي"))
                .ToListAsync();

            decimal total = 0;

            foreach (var acc in cashAccounts)
            {
                var balance = await db.LedgerEntries
                    .Where(x => x.AccountId == acc.Id)
                    .OrderByDescending(x => x.Id)
                    .Select(x => x.Balance)
                    .FirstOrDefaultAsync();

                total += balance;
            }

            return total;
        }


    }
}

