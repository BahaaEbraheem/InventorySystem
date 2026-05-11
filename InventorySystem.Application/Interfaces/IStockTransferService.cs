using InventorySystem.Application.DTOs.Transfers;
using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Application.Interfaces
{
    public interface IStockTransferService
    {
        Task<CreateStockTransferResponse> CreateTransferAsync(CreateStockTransferRequest request, CancellationToken cancellationToken = default);
        Task<StockTransferDetailDto?> GetTransferByIdAsync(
        Guid transferId,
        CancellationToken cancellationToken = default);
    }
}
