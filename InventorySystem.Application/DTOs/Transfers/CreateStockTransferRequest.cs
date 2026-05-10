using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Application.DTOs.Transfers;

public class CreateStockTransferRequest
{
    public Guid FromWarehouseId { get; set; }
    public Guid ToWarehouseId { get; set; }
    public DateTime TransferDate { get; set; }
    public List<CreateStockTransferItemDto> Items { get; set; } = new();
}

