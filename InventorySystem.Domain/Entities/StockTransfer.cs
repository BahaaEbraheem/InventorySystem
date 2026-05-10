using InventorySystem.Domain.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Domain.Entities
{
    public class StockTransfer : AuditableEntity
    {
        public Guid FromWarehouseId { get; set; }
        public Warehouse FromWarehouse { get; set; } = default!;

        public Guid ToWarehouseId { get; set; }
        public Warehouse ToWarehouse { get; set; } = default!;

        public DateTime TransferDate { get; set; }

        public ICollection<StockTransferItem> Items { get; set; } = new List<StockTransferItem>();
    }
}
