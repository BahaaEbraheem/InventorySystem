using InventorySystem.Application.DTOs.Sales;
using InventorySystem.Application.Interfaces;
using InventorySystem.Infrastructure.Persistence;
using InventorySystem.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SalesController : ControllerBase
{
    private readonly ISalesService _salesService;

    public SalesController(ISalesService salesService)
    {
        _salesService = salesService;
    }

    /// <summary>
    /// Create a new sale. Stock is allocated automatically using FIFO from available batches.
    /// </summary>
    /// <remarks>
    /// - Allocates stock from oldest batches first (FIFO).
    /// - Uses row-level locking to prevent concurrent overselling.
    /// - Provide a unique IdempotencyKey to prevent duplicate submissions.
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(BaseResponse<CreateSaleResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(BaseResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(BaseResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(BaseResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BaseResponse<CreateSaleResponse>>> Create(
        [FromBody] CreateSaleRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Items == null || !request.Items.Any())
            return BadRequest(BaseResponse<object>.ErrorResponse(
                ResponseCodes.ValidationError,
                "Sale must contain at least one item."));

        var result = await _salesService.CreateSaleAsync(request, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.SaleId },
            BaseResponse<CreateSaleResponse>.SuccessResponse(result, "Sale created successfully."));
    }

    /// <summary>
    /// Get sale details including all batch allocations (full traceability per Problem #1).
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BaseResponse<SaleDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BaseResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BaseResponse<SaleDetailDto>>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var sale = await _salesService.GetSaleByIdAsync(id, cancellationToken);

        if (sale == null)
            return NotFound(BaseResponse<object>.ErrorResponse(
                ResponseCodes.NotFound,
                $"Sale with id '{id}' not found."));

        return Ok(BaseResponse<SaleDetailDto>.SuccessResponse(sale, "Sale retrieved successfully."));
    }
}

