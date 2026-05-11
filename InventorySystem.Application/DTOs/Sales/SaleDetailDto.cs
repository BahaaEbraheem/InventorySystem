namespace InventorySystem.Application.DTOs.Sales;


public class SaleDetailDto
{
    public Guid Id { get; set; }
    public DateTime SaleDate { get; set; }
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = default!;
    public List<SaleItemDetailDto> Items { get; set; } = new();
}

public class SaleItemDetailDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = default!;
    public decimal Quantity { get; set; }

    /// <summary>
    /// Traceability: which batches (supplier/PO) were consumed for this sale item.
    /// </summary>
    public List<BatchAllocationDto> BatchAllocations { get; set; } = new();
}

public class BatchAllocationDto
{
    public Guid StockBatchId { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = default!;
    public Guid PurchaseOrderItemId { get; set; }
    public DateTime PurchaseDate { get; set; }
    public decimal AllocatedQuantity { get; set; }
}