using InventorySystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StockBatchesController : ControllerBase
{
    private readonly AppDbContext _db;

    public StockBatchesController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var batches = await _db.StockBatches
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return Ok(batches);
    }
}
