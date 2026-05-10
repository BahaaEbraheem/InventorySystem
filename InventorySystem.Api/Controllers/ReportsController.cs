using InventorySystem.Application.DTOs.Reporting;
using InventorySystem.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace InventorySystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly IReportingService _reportingService;

    public ReportsController(IReportingService reportingService)
    {
        _reportingService = reportingService;
    }

    [HttpGet("sales")]
    public async Task<ActionResult<List<SalesReportItemDto>>> GetSalesReport(
        [FromQuery] Guid? warehouseId,
        [FromQuery] Guid? supplierId,
        [FromQuery] Guid? productCategoryId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken cancellationToken)
    {
        var filter = new SalesReportFilter
        {
            WarehouseId = warehouseId,
            SupplierId = supplierId,
            ProductCategoryId = productCategoryId,
            FromDate = fromDate,
            ToDate = toDate
        };

        var result = await _reportingService.GetSalesReportAsync(filter, cancellationToken);
        return Ok(result);
    }
}
