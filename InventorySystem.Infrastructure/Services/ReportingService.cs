using InventorySystem.Application.DTOs.Reporting;
using InventorySystem.Application.Interfaces;
using InventorySystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Infrastructure.Services;

public class ReportingService : IReportingService
{
    private readonly AppDbContext _dbContext;

    public ReportingService(AppDbContext dbContext) => _dbContext = dbContext;

    public async Task<List<SalesReportItemDto>> GetSalesReportAsync(
        SalesReportFilter filter, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.SaleItemBatchAllocations
            .AsNoTracking()
            .Include(a => a.SaleItem.Sale)
            .Include(a => a.SaleItem.Product)
            .Include(a => a.StockBatch.Supplier)
            .Include(a => a.StockBatch.Warehouse)
           .Where(a => a.SaleItem.Sale.IsActive) ;

        // تطبيق الفلاتر البسيطة
        if (filter.WarehouseId.HasValue)
            query = query.Where(a => a.SaleItem.Sale.WarehouseId == filter.WarehouseId);

        if (filter.SupplierId.HasValue)
            query = query.Where(a => a.StockBatch.SupplierId == filter.SupplierId);

        if (filter.FromDate.HasValue)
            query = query.Where(a => a.SaleItem.Sale.SaleDate >= filter.FromDate);

        if (filter.ToDate.HasValue)
            query = query.Where(a => a.SaleItem.Sale.SaleDate <= filter.ToDate);

        // فلتر التصنيف (استعلام بسيط)
        if (filter.ProductCategoryId.HasValue)
        {
            var productIds = await _dbContext.Products
                .Where(p => p.CategoryId == filter.ProductCategoryId && p.IsActive)
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);

            query = query.Where(a => productIds.Contains(a.SaleItem.ProductId));
        }

        // التجميع والإرجاع
        return await query
            .GroupBy(a => new
            {
                ProductId = a.SaleItem.ProductId,
                ProductName = a.SaleItem.Product.Name,           // ✅ اسم صريح
                SupplierId = a.StockBatch.SupplierId,
                SupplierName = a.StockBatch.Supplier.Name,       // ✅ اسم صريح
                WarehouseId = a.StockBatch.WarehouseId,
                WarehouseName = a.StockBatch.Warehouse.Name      // ✅ اسم صريح
            })
            .Select(g => new SalesReportItemDto
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.ProductName,
                SupplierId = g.Key.SupplierId,
                SupplierName = g.Key.SupplierName,
                WarehouseId = g.Key.WarehouseId,
                WarehouseName = g.Key.WarehouseName,
                QuantitySold = g.Sum(a => a.Quantity),
                FirstSaleDate = g.Min(a => a.SaleItem.Sale.SaleDate),
                LastSaleDate = g.Max(a => a.SaleItem.Sale.SaleDate)
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<ShipmentStockDto> GetRemainingStockFromShipmentAsync(
        Guid purchaseOrderItemId, CancellationToken cancellationToken = default)
    {
        var batches = await _dbContext.StockBatches
            .AsNoTracking()
            .Where(b => b.PurchaseOrderItemId == purchaseOrderItemId && b.IsActive)
            .Select(b => new { b.WarehouseId, b.QuantityRemaining, b.QuantityReserved })
            .ToListAsync(cancellationToken);

        return new ShipmentStockDto
        {
            PurchaseOrderItemId = purchaseOrderItemId,
            TotalRemaining = batches.Sum(b => b.QuantityRemaining),
            TotalReserved = batches.Sum(b => b.QuantityReserved),
            ByWarehouse = batches
                .GroupBy(b => b.WarehouseId)
                .Select(g => new WarehouseStockSummary
                {
                    WarehouseId = g.Key,
                    Quantity = g.Sum(b => b.QuantityRemaining)
                })
                .ToList()
        };
    }
}