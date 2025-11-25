using Microsoft.AspNetCore.Mvc;
using WebAPI.Services;

namespace WebAPI.Controllers
{
    [ApiController]
    [Produces("application/json")]
    [Route("api/{project}/reports")]
    public class ReportsController : ControllerBase
    {
        private readonly AccountingService _service;
        public ReportsController(AccountingService service) => _service = service;

        [HttpGet("trial-balance")]
        public async Task<IActionResult> TrialBalance(string project)
        {
            var tb = await _service.GetTrialBalance(project);
            return Ok(new { list = tb });
        }

        [HttpGet("income-statement")]
        public async Task<IActionResult> IncomeStatement(string project, DateTime? from = null, DateTime? to = null)
        {
            var res = await _service.GetIncomeStatement(project, from, to);
            return Ok(new { list = res });
        }

        [HttpGet("balance-sheet")]
        public async Task<IActionResult> BalanceSheet(string project)
        {
            var res = await _service.GetBalanceSheet(project);
            return Ok(new { list = res });
        }

        [HttpGet("ledger/{accountId}")]
        public async Task<IActionResult> Ledger(string project, int accountId)
        {
            var res = await _service.GetLedgerForAccount(project, accountId);
            return Ok(new { list = res });
        }

        [HttpGet("generaledger")]
        public async Task<IActionResult> GeneraLedger(string project)
        { 
            var res = await _service.GetLedgerForProject(project);
            return Ok(new { list = res });
        }
        [HttpGet("cash-flow")]
        public async Task<IActionResult> CashFlow(string project, DateTime? from = null, DateTime? to = null)
        {
            var res = await _service.GetCashFlowForAllAccounts(project, from, to);
            return Ok(new { list = res });
        }
        [HttpGet("available-cash")]
        public async Task<IActionResult> AvailableCash(string project)
        {
            var cash = await _service.GetAvailableCash(project);
            return Ok(new { availableCash = cash });
        }

    }
}
