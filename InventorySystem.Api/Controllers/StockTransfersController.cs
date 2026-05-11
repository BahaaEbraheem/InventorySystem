using InventorySystem.Application.DTOs.Transfers;
using InventorySystem.Application.Interfaces;
using InventorySystem.Infrastructure.Persistence;
using InventorySystem.Shared.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class StockTransfersController : ControllerBase
{
    private readonly IStockTransferService _transferService;

    public StockTransfersController(IStockTransferService transferService)
    {
        _transferService = transferService;
    }

    /// <summary>
    /// Transfer stock between two warehouses atomically.
    /// </summary>
    /// <remarks>
    /// - The entire transfer (deduct from source + add to destination) runs in a single atomic transaction.
    /// - If any step fails, a full ROLLBACK occurs — no partial state is persisted.
    /// - Provide a unique IdempotencyKey to prevent duplicate transfers on retry.
    /// - Source and destination warehouses must be different.
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(BaseResponse<CreateStockTransferResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(BaseResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(BaseResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(BaseResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BaseResponse<CreateStockTransferResponse>>> Create(
        [FromBody] CreateStockTransferRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Items == null || !request.Items.Any())
            return BadRequest(BaseResponse<object>.ErrorResponse(
                ResponseCodes.ValidationError,
                "Transfer must contain at least one item."));

        if (request.FromWarehouseId == request.ToWarehouseId)
            return BadRequest(BaseResponse<object>.ErrorResponse(
                ResponseCodes.ValidationError,
                "Source and destination warehouses must be different."));

        var result = await _transferService.CreateTransferAsync(request, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.StockTransferId },
            BaseResponse<CreateStockTransferResponse>.SuccessResponse(
                result, "Stock transfer completed successfully."));
    }

    /// <summary>
    /// Get transfer details including items and warehouse info.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BaseResponse<StockTransferDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BaseResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BaseResponse<StockTransferDetailDto>>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var transfer = await _transferService.GetTransferByIdAsync(id, cancellationToken);

        if (transfer == null)
            return NotFound(BaseResponse<object>.ErrorResponse(
                ResponseCodes.NotFound,
                $"Stock transfer with id '{id}' not found."));

        return Ok(BaseResponse<StockTransferDetailDto>.SuccessResponse(
            transfer, "Transfer retrieved successfully."));
    }
}

