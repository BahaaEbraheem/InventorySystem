using InventorySystem.Application.DTOs.Transfers;
using InventorySystem.Application.Interfaces;
using InventorySystem.Domain.Entities;
using InventorySystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Infrastructure.Services;

public class StockTransferService : IStockTransferService
{
    private readonly AppDbContext _dbContext;

    public StockTransferService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CreateStockTransferResponse> CreateTransferAsync(CreateStockTransferRequest request, CancellationToken cancellationToken = default)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);

        var transfer = new StockTransfer
        {
            Id = Guid.NewGuid(),
            FromWarehouseId = request.FromWarehouseId,
            ToWarehouseId = request.ToWarehouseId,
            TransferDate = request.TransferDate,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "system"
        };

        foreach (var item in request.Items)
        {
            var transferItem = new StockTransferItem
            {
                Id = Guid.NewGuid(),
                StockTransfer = transfer,
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            };

            transfer.Items.Add(transferItem);

            // 1) خصم من المخزن المصدر (FIFO من StockBatch)
            var sourceBatches = await _dbContext.StockBatches
                .Where(b =>
                    b.ProductId == item.ProductId &&
                    b.WarehouseId == request.FromWarehouseId &&
                    b.QuantityRemaining > 0)
                .OrderBy(b => b.PurchaseDate)
                .ThenBy(b => b.CreatedAt)
                .ToListAsync(cancellationToken);

            var totalAvailable = sourceBatches.Sum(b => b.QuantityRemaining);
            if (totalAvailable < item.Quantity)
                throw new InvalidOperationException($"Insufficient stock in source warehouse for product {item.ProductId}.");

            var remainingToMove = item.Quantity;

            foreach (var batch in sourceBatches)
            {
                if (remainingToMove <= 0)
                    break;

                var moveQty = Math.Min(batch.QuantityRemaining, remainingToMove);
                batch.QuantityRemaining -= moveQty;
                remainingToMove -= moveQty;

                // 2) إنشاء Batch جديد في المخزن الوجهة
                var destBatch = new StockBatch
                {
                    Id = Guid.NewGuid(),
                    ProductId = batch.ProductId,
                    SupplierId = batch.SupplierId,
                    WarehouseId = request.ToWarehouseId,
                    PurchaseOrderItemId = batch.PurchaseOrderItemId,
                    QuantityReceived = moveQty,
                    QuantityRemaining = moveQty,
                    PurchaseDate = batch.PurchaseDate,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "system"
                };

                await _dbContext.StockBatches.AddAsync(destBatch, cancellationToken);
                _dbContext.StockBatches.Update(batch);
            }
        }

        await _dbContext.StockTransfers.AddAsync(transfer, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CreateStockTransferResponse { StockTransferId = transfer.Id };
    }
}
