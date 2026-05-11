using InventorySystem.Application.DTOs.Reporting;

namespace InventorySystem.Application.Interfaces;

public interface IReportingService
{
    Task<List<SalesReportItemDto>> GetSalesReportAsync(SalesReportFilter filter, CancellationToken cancellationToken = default);
    Task<ShipmentStockDto> GetRemainingStockFromShipmentAsync(
    Guid purchaseOrderItemId,
    CancellationToken cancellationToken = default);
}
