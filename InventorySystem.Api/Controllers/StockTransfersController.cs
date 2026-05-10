using InventorySystem.Application.DTOs.Transfers;
using InventorySystem.Application.Interfaces;
using InventorySystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StockTransfersController : ControllerBase
{
    private readonly IStockTransferService _transferService;

    public StockTransfersController(IStockTransferService transferService)
    {
        _transferService = transferService;
    }

    [HttpPost]
    public async Task<ActionResult<CreateStockTransferResponse>> Create(CreateStockTransferRequest request, CancellationToken cancellationToken)
    {
        if (request.Items == null || !request.Items.Any())
            return BadRequest("Transfer must contain at least one item.");

        var result = await _transferService.CreateTransferAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.StockTransferId }, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> GetById(Guid id, [FromServices] AppDbContext dbContext)
    {
        var transfer = await dbContext.StockTransfers
            .Include(t => t.Items)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (transfer == null)
            return NotFound();

        return Ok(transfer);
    }
}
