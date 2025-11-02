using Microsoft.AspNetCore.Mvc;
using WebAPI.Data.Entities;
using WebAPI.Data;
using Microsoft.EntityFrameworkCore;

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
            return Ok(list);
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
        public async Task<IActionResult> Create(string project, [FromBody] ChartOfAccount model)
        {
            using var db = _factory.Create(project);
            await db.ChartOfAccounts.AddAsync(model);
            await db.SaveChangesAsync();
            return CreatedAtAction(nameof(Get), new { project, id = model.Id }, model);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string project, int id, [FromBody] ChartOfAccount model)
        {
            using var db = _factory.Create(project);
            var existing = await db.ChartOfAccounts.FindAsync(id);
            if (existing == null) return NotFound();
            existing.AccountCode = model.AccountCode;
            existing.AccountName = model.AccountName;
            existing.AccountType = model.AccountType;
            existing.ParentAccountId = model.ParentAccountId;
            existing.IsDetail = model.IsDetail;
            await db.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string project, int id)
        {
            using var db = _factory.Create(project);
            var ex = await db.ChartOfAccounts.FindAsync(id);
            if (ex == null) return NotFound();
            db.ChartOfAccounts.Remove(ex);
            await db.SaveChangesAsync();
            return NoContent();
        }
    }
}
