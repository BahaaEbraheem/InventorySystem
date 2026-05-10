using InventorySystem.Application.DTOs.Sales;
using InventorySystem.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using InventorySystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SalesController : ControllerBase
{
    private readonly ISalesService _salesService;

    public SalesController(ISalesService salesService)
    {
        _salesService = salesService;
    }

    [HttpPost]
    public async Task<ActionResult<CreateSaleResponse>> Create([FromBody] CreateSaleRequest request, CancellationToken cancellationToken)
    {
        if (request.Items == null || !request.Items.Any())
            return BadRequest("Sale must contain at least one item.");

        var result = await _salesService.CreateSaleAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.SaleId }, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> GetById(Guid id, [FromServices] AppDbContext dbContext)
    {
        var sale = await dbContext.Sales
            .Include(s => s.Items)
                .ThenInclude(i => i.BatchAllocations)
                    .ThenInclude(a => a.StockBatch)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (sale == null)
            return NotFound();

        return Ok(sale);
    }
}
