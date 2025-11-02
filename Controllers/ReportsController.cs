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
            return Ok(tb);
        }

        [HttpGet("income-statement")]
        public async Task<IActionResult> IncomeStatement(string project, DateTime? from = null, DateTime? to = null)
        {
            var res = await _service.GetIncomeStatement(project, from, to);
            return Ok(res);
        }

        [HttpGet("balance-sheet")]
        public async Task<IActionResult> BalanceSheet(string project)
        {
            var res = await _service.GetBalanceSheet(project);
            return Ok(res);
        }

        [HttpGet("ledger/{accountId}")]
        public async Task<IActionResult> Ledger(string project, int accountId)
        {
            var res = await _service.GetLedgerForAccount(project, accountId);
            return Ok(res);
        }
    }
}
