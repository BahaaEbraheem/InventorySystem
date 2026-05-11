using InventorySystem.Domain.Common;
using InventorySystem.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Domain.Entities
{
    public class StockTransfer : AuditableEntity
    {
        public Guid FromWarehouseId { get; set; }
        public Warehouse FromWarehouse { get; set; } = default!;
        public StockTransferStatus Status { get; set; } = StockTransferStatus.Pending;
        public DateTime? CompletedAt { get; set; }
        public Guid IdempotencyKey { get; set; }


        public Guid ToWarehouseId { get; set; }
        public Warehouse ToWarehouse { get; set; } = default!;

        public DateTime TransferDate { get; set; }

        public ICollection<StockTransferItem> Items { get; set; } = new List<StockTransferItem>();


        public bool CanBeCancelled() =>
    Status is StockTransferStatus.Pending or StockTransferStatus.Picked;
    }
}
