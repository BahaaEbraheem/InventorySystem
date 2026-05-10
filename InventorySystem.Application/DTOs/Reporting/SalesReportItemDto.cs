using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Application.DTOs.Reporting;

public class SalesReportItemDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = default!;
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = default!;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = default!;
    public decimal QuantitySold { get; set; }
    public DateTime FirstSaleDate { get; set; }
    public DateTime LastSaleDate { get; set; }
}
