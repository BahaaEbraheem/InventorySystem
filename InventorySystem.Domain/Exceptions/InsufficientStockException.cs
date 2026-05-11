using InventorySystem.Shared.Responses;
using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Domain.Exceptions
{
    public class InsufficientStockException : ApplicationException
    {
        public Guid ProductId { get; }
        public Guid WarehouseId { get; }
        public decimal Requested { get; }
        public decimal Available { get; }

        public InsufficientStockException(
            Guid productId,
            Guid warehouseId,
            decimal requested,
            decimal available)
            : base(
                $"Insufficient stock. Product: {productId}, Warehouse: {warehouseId}, Requested: {requested}, Available: {available}",
                ResponseCodes.InsufficientStock,
                409,
                new()
                {
                { "productId", productId.ToString() },
                { "warehouseId", warehouseId.ToString() },
                { "requested", requested.ToString() },
                { "available", available.ToString() }
                })
        {
            ProductId = productId;
            WarehouseId = warehouseId;
            Requested = requested;
            Available = available;
        }
    }
}
