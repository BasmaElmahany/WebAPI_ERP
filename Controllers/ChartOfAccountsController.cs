using Microsoft.AspNetCore.Mvc;
using WebAPI.Data.Entities;
using WebAPI.Data;
using Microsoft.EntityFrameworkCore;
using WebAPI.Models;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace WebAPI.Controllers
{

    [ApiController]
    [Produces("application/json")]
    [Route("api/{project}/chart-of-accounts")]
    public class ChartOfAccountsController : ControllerBase
    {
        private readonly ProjectDbContextFactory _factory;
        public ChartOfAccountsController(ProjectDbContextFactory factory) => _factory = factory;

        [HttpGet]
        public async Task<IActionResult> GetAll(string project)
        {
            using var db = _factory.Create(project);
            var list = await db.ChartOfAccounts.OrderBy(x => x.AccountCode).ToListAsync();
            return Ok(new { list });
        }
        [HttpGet("List")]
        public async Task<IActionResult> GetAccountList(string project)
        {
            using var db = _factory.Create(project);
            var list = await db.ChartOfAccounts.Select(c=> new
            {
                c.Id ,
                c.AccountName
            }).ToListAsync();
            return Ok(new { list });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(string project, int id)
        {
            using var db = _factory.Create(project);
            var item = await db.ChartOfAccounts.FindAsync(id);
            if (item == null) return NotFound();
            return Ok(item);
        }
        [HttpPost]
        public async Task<IActionResult> Create(string project, [FromBody] AccountWithChartDto dto)
        {
            using var db = _factory.Create(project);

            var chart = new ChartOfAccount
            {
                AccountCode = dto.AccountCode,
                AccountName = dto.AccountName,
                AccountType = dto.AccountType,
                ParentAccountId = dto.ParentAccountId,
                IsDetail = dto.IsDetail
            };

            await db.ChartOfAccounts.AddAsync(chart);
            await db.SaveChangesAsync();

            var account = new Account
            {
                AccountId = chart.Id,
                Currency = dto.Currency,
                OpeningBalance = dto.OpeningBalance,
                Balance = dto.OpeningBalance // ⭐ strongly required
            };

            await db.Accounts.AddAsync(account);
            await db.SaveChangesAsync();

            return CreatedAtAction(nameof(Get), new { project, id = chart.Id }, new { Chart = chart, Account = account });
        }




        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string project, int id, [FromBody] AccountWithChartDto dto)
        {
            using var db = _factory.Create(project);

            var chart = await db.ChartOfAccounts.FindAsync(id);
            if (chart == null) return NotFound();

            chart.AccountCode = dto.AccountCode;
            chart.AccountName = dto.AccountName;
            chart.AccountType = dto.AccountType;
            chart.ParentAccountId = dto.ParentAccountId;
            chart.IsDetail = dto.IsDetail;

            var account = await db.Accounts.FirstOrDefaultAsync(a => a.AccountId == id);

            if (account == null)
            {
                account = new Account
                {
                    AccountId = id,
                    Currency = dto.Currency,
                    OpeningBalance = dto.OpeningBalance,
                    Balance = dto.OpeningBalance // new account
                };
                await db.Accounts.AddAsync(account);
            }
            else
            {
                account.Currency = dto.Currency;

                // ⭐ Only allow updating opening balance if no ledger activity
                bool hasLedger = await db.LedgerEntries.AnyAsync(l => l.AccountId == id);

                if (!hasLedger)
                {
                    account.OpeningBalance = dto.OpeningBalance;
                    account.Balance = dto.OpeningBalance;
                }
            }

            await db.SaveChangesAsync();
            return NoContent();
        }


        [HttpGet("{id}/dto")]
        public async Task<IActionResult> GetDto(string project, int id)
        {
            using var db = _factory.Create(project);

            var chart = await db.ChartOfAccounts.FindAsync(id);
            if (chart == null) return NotFound();

            var account = await db.Accounts.FirstOrDefaultAsync(x => x.AccountId == id);

            return Ok(new AccountWithChartDto
            {
                AccountCode = chart.AccountCode,
                AccountName = chart.AccountName,
                AccountType = chart.AccountType,
                ParentAccountId = chart.ParentAccountId,
                IsDetail = chart.IsDetail,

                Currency = account?.Currency ?? "EGP",
                OpeningBalance = account?.OpeningBalance ?? 0

            });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string project, int id)
        {
            using var db = _factory.Create(project);

            var chart = await db.ChartOfAccounts.FindAsync(id);
            if (chart == null) return NotFound();

            var account = db.Accounts.FirstOrDefault(x => x.AccountId == id);
            if (account != null) db.Accounts.Remove(account);

            db.ChartOfAccounts.Remove(chart);
            await db.SaveChangesAsync();

            return NoContent();
        }


    }
}
