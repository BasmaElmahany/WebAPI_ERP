using WebAPI.Data.Entities;
using WebAPI.Data;
using Microsoft.EntityFrameworkCore;

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
            if (entry.Posted) { return; }

            var lines = await db.JournalLines.Where(x => x.JournalEntryId == journalEntryId).ToListAsync();

            // Basic validation: debits == credits
            var totalDebit = lines.Sum(l => l.Debit);
            var totalCredit = lines.Sum(l => l.Credit);
            if (totalDebit != totalCredit)
                throw new InvalidOperationException("Journal entry not balanced: total debit != total credit");

            foreach (var line in lines)
            {
                var last = await db.LedgerEntries
                    .Where(l => l.AccountId == line.AccountId)
                    .OrderByDescending(l => l.Date).ThenByDescending(l => l.Id)
                    .FirstOrDefaultAsync();

                decimal prevBalance = last?.Balance ?? 0m;
                decimal newBalance = prevBalance + line.Debit - line.Credit;

                var ledger = new LedgerEntry
                {
                    AccountId = line.AccountId,
                    Date = entry.Date,
                    JournalEntryId = entry.Id,
                    Description = line.Description ?? entry.Description,
                    Debit = line.Debit,
                    Credit = line.Credit,
                    Balance = newBalance
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

            var entry = await db.JournalEntries
               // .Include(e => e.Lines)
                .FirstOrDefaultAsync(e => e.Id == journalEntryId);

            if (entry == null)
                throw new InvalidOperationException("Journal entry not found.");

            if (!entry.Posted)
                throw new InvalidOperationException("This journal entry is not posted.");

            // ✅ حذف تأثير القيد من الأستاذ العام (Ledger)
            var ledgerLines = db.LedgerEntries
                .Where(l => l.JournalEntryId == entry.Id);

            db.LedgerEntries.RemoveRange(ledgerLines);

            // ✅ تعديل حالة القيد
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
        public async Task<object> GetIncomeStatement(string projectSchema, DateTime? from = null, DateTime? to = null)
        {
            using var db = _factory.Create(projectSchema);
            var revQ = db.Revenues.AsQueryable();
            var expQ = db.Expenses.AsQueryable();

            if (from.HasValue) { revQ = revQ.Where(r => r.Date >= from.Value); expQ = expQ.Where(e => e.Date >= from.Value); }
            if (to.HasValue) { revQ = revQ.Where(r => r.Date <= to.Value); expQ = expQ.Where(e => e.Date <= to.Value); }

            var totalRev = await revQ.SumAsync(r => (decimal?)r.Amount) ?? 0m;
            var totalExp = await expQ.SumAsync(e => (decimal?)e.Amount) ?? 0m;

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

            var ledgerGroup = from l in db.LedgerEntries
                              join c in db.ChartOfAccounts on l.AccountId equals c.Id
                              group l by c.AccountType into g
                              select new
                              {
                                  AccountType = g.Key,
                                  Balance = g.Sum(x => x.Debit - x.Credit)
                              };

            var list = await ledgerGroup.ToListAsync();
            var assets = list.FirstOrDefault(x => x.AccountType == "Asset")?.Balance ?? 0m;
            var liabilities = list.FirstOrDefault(x => x.AccountType == "Liability")?.Balance ?? 0m;
            var equity = list.FirstOrDefault(x => x.AccountType == "Equity")?.Balance ?? 0m;

            return new
            {
                Assets = assets,
                Liabilities = liabilities,
                Equity = equity,
                IsBalanced = Math.Round(assets, 2) == Math.Round((liabilities + equity), 2)
            };
        }

        // Ledger for account
        public async Task<IEnumerable<LedgerEntry>> GetLedgerForAccount(string projectSchema, int accountId)
        {
            using var db = _factory.Create(projectSchema);
            return await db.LedgerEntries.Where(l => l.AccountId == accountId).OrderBy(l => l.Date).ThenBy(l => l.Id).ToListAsync();
        }
    }
}

