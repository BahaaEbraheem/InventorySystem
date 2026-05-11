using InventorySystem.Application.DTOs.Reporting;
using InventorySystem.Application.Interfaces;
using InventorySystem.Shared.Responses;
using Microsoft.AspNetCore.Mvc;

namespace InventorySystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ReportsController : ControllerBase
{
    private readonly IReportingService _reportingService;

    public ReportsController(IReportingService reportingService)
    {
        _reportingService = reportingService;
    }

    /// <summary>
    /// Get a sales report filtered by warehouse, supplier, product category, and/or date range.
    /// </summary>
    /// <remarks>
    /// All query parameters are optional — omit any to remove that filter.
    ///
    /// Results are grouped by Product + Supplier + Warehouse and include:
    /// - Total quantity sold
    /// - First and last sale dates
    ///
    /// Indexes on SupplierId, WarehouseId, ProductCategoryId, and SaleDate ensure
    /// this endpoint performs well even with large datasets.
    ///
    /// Example:
    ///
    ///     GET /api/reports/sales?supplierId=abc&amp;fromDate=2024-01-01&amp;toDate=2024-03-31
    /// </remarks>
    /// <param name="warehouseId">Filter by warehouse</param>
    /// <param name="supplierId">Filter by supplier (answers: "how much did we sell from Supplier X?")</param>
    /// <param name="productCategoryId">Filter by product category</param>
    /// <param name="fromDate">Start of date range (inclusive)</param>
    /// <param name="toDate">End of date range (inclusive)</param>
    [HttpGet("sales")]
    [ProducesResponseType(typeof(BaseResponse<List<SalesReportItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BaseResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(BaseResponse<object>), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BaseResponse<List<SalesReportItemDto>>>> GetSalesReport(
        [FromQuery] Guid? warehouseId,
        [FromQuery] Guid? supplierId,
        [FromQuery] Guid? productCategoryId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken cancellationToken)
    {
        if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
            return BadRequest(BaseResponse<object>.ErrorResponse(
                ResponseCodes.ValidationError,
                "fromDate must be earlier than or equal to toDate."));

        var filter = new SalesReportFilter
        {
            WarehouseId = warehouseId,
            SupplierId = supplierId,
            ProductCategoryId = productCategoryId,
            FromDate = fromDate,
            ToDate = toDate
        };

        var result = await _reportingService.GetSalesReportAsync(filter, cancellationToken);

        return Ok(BaseResponse<List<SalesReportItemDto>>.SuccessResponse(
            result,
            $"Report generated successfully. {result.Count} record(s) found."));
    }

    /// <summary>
    /// Get remaining stock for a specific shipment (PurchaseOrderItem).
    /// Answers: "What is our remaining stock from the shipment we received last March?"
    /// </summary>
    /// <param name="purchaseOrderItemId">The PurchaseOrderItem ID representing the shipment</param>
    [HttpGet("shipment-stock/{purchaseOrderItemId:guid}")]
    [ProducesResponseType(typeof(BaseResponse<ShipmentStockDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BaseResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BaseResponse<ShipmentStockDto>>> GetShipmentStock(
        Guid purchaseOrderItemId,
        CancellationToken cancellationToken)
    {
        var result = await _reportingService.GetRemainingStockFromShipmentAsync(
            purchaseOrderItemId, cancellationToken);

        if (result == null)
            return NotFound(BaseResponse<object>.ErrorResponse(
                ResponseCodes.NotFound,
                $"No shipment data found for PurchaseOrderItem '{purchaseOrderItemId}'."));

        return Ok(BaseResponse<ShipmentStockDto>.SuccessResponse(
            result,
            "Shipment stock retrieved successfully."));
    }
}

