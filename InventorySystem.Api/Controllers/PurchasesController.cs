using InventorySystem.Application.DTOs.Purchase;
using InventorySystem.Application.Interfaces;
using InventorySystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PurchasesController : ControllerBase
{
    private readonly IPurchaseService _purchaseService;

    public PurchasesController(IPurchaseService purchaseService)
    {
        _purchaseService = purchaseService;
    }

    [HttpPost]
    public async Task<ActionResult<CreatePurchaseOrderResponse>> Create([FromBody] CreatePurchaseOrderRequest request, CancellationToken cancellationToken)
    {
        if (request.Items == null || !request.Items.Any())
            return BadRequest("Purchase order must contain at least one item.");

        var result = await _purchaseService.CreatePurchaseOrderAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.PurchaseOrderId }, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> GetById(Guid id, [FromServices] AppDbContext dbContext)
    {
        var po = await dbContext.PurchaseOrders
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (po == null)
            return NotFound();

        return Ok(po);
    }
}
