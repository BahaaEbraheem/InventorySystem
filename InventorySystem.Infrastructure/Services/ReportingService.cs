using InventorySystem.Application.DTOs.Reporting;
using InventorySystem.Application.Interfaces;
using InventorySystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Infrastructure.Services;

public class ReportingService : IReportingService
{
    private readonly AppDbContext _dbContext;

    public ReportingService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<SalesReportItemDto>> GetSalesReportAsync(SalesReportFilter filter, CancellationToken cancellationToken = default)
    {
        var query =
            from sale in _dbContext.Sales
            from item in sale.Items
            from alloc in item.BatchAllocations
            join batch in _dbContext.StockBatches on alloc.StockBatchId equals batch.Id
            join product in _dbContext.Products on item.ProductId equals product.Id
            join supplier in _dbContext.Suppliers on batch.SupplierId equals supplier.Id
            join warehouse in _dbContext.Warehouses on sale.WarehouseId equals warehouse.Id
            select new
            {
                sale.SaleDate,
                sale.WarehouseId,
                WarehouseName = warehouse.Name,
                item.ProductId,
                ProductName = product.Name,
                SupplierId = supplier.Id,
                SupplierName = supplier.Name,
                Quantity = alloc.Quantity,
                CategoryId = product.CategoryId
            };

        if (filter.WarehouseId.HasValue)
            query = query.Where(x => x.WarehouseId == filter.WarehouseId.Value);

        if (filter.SupplierId.HasValue)
            query = query.Where(x => x.SupplierId == filter.SupplierId.Value);

        if (filter.ProductCategoryId.HasValue)
            query = query.Where(x => x.CategoryId == filter.ProductCategoryId.Value);

        if (filter.FromDate.HasValue)
            query = query.Where(x => x.SaleDate >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(x => x.SaleDate <= filter.ToDate.Value);

        var grouped = await query
            .GroupBy(x => new
            {
                x.ProductId,
                x.ProductName,
                x.SupplierId,
                x.SupplierName,
                x.WarehouseId,
                x.WarehouseName
            })
            .Select(g => new SalesReportItemDto
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.ProductName,
                SupplierId = g.Key.SupplierId,
                SupplierName = g.Key.SupplierName,
                WarehouseId = g.Key.WarehouseId,
                WarehouseName = g.Key.WarehouseName,
                QuantitySold = g.Sum(x => x.Quantity),
                FirstSaleDate = g.Min(x => x.SaleDate),
                LastSaleDate = g.Max(x => x.SaleDate)
            })
            .ToListAsync(cancellationToken);

        return grouped;
    }
}
