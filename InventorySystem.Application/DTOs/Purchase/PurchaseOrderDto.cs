using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Application.DTOs.Purchase
{
    public class PurchaseOrderDto
    {
        public Guid Id { get; set; }
        public Guid SupplierId { get; set; }
        public string? SupplierName { get; set; }
        public DateTime PurchaseDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public decimal TotalAmount { get; set; }
        public int ItemsCount => Items?.Count ?? 0;

        public List<PurchaseOrderItemDto> Items { get; set; } = new();
    }
}
