using InventorySystem.Application.DTOs.Sales;
using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Application.Interfaces
{
    public interface ISalesService
    {
        Task<CreateSaleResponse> CreateSaleAsync(CreateSaleRequest request, CancellationToken cancellationToken = default);
    }
}
