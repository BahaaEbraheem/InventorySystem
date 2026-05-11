using InventorySystem.Domain.Entities;
using InventorySystem.Shared.Enums;
using Xunit;

namespace InventorySystem.Tests.Unit.Domain;

/// <summary>
/// Unit tests for pure domain entity logic — no database, no mocks.
/// Tests computed properties and business rule methods on entities.
/// </summary>
public class EntityLogicTests
{
    // ─── StockBatch ──────────────────────────────────────────────

    [Fact]
    public void StockBatch_QuantityAvailable_ShouldBeRemainingMinusReserved()
    {
        var batch = new StockBatch
        {
            QuantityRemaining = 100,
            QuantityReserved = 30
        };

        Assert.Equal(70, batch.QuantityAvailable);
    }

    [Fact]
    public void StockBatch_QuantityAvailable_WhenNoReservations_ShouldEqualRemaining()
    {
        var batch = new StockBatch { QuantityRemaining = 50, QuantityReserved = 0 };

        Assert.Equal(50, batch.QuantityAvailable);
    }

    [Fact]
    public void StockBatch_IsFullyReceived_WhenReceivedEqualsOrdered_ShouldBeTrue()
    {
        var batch = new StockBatch { QuantityReceived = 100, OrderedQuantity = 100 };

        Assert.True(batch.IsFullyReceived);
    }

    [Fact]
    public void StockBatch_IsFullyReceived_WhenReceivedLessThanOrdered_ShouldBeFalse()
    {
        var batch = new StockBatch { QuantityReceived = 60, OrderedQuantity = 100 };

        Assert.False(batch.IsFullyReceived);
    }

    [Fact]
    public void StockBatch_IsFullyReceived_WhenReceivedExceedsOrdered_ShouldBeTrue()
    {
        // Over-delivery edge case
        var batch = new StockBatch { QuantityReceived = 110, OrderedQuantity = 100 };

        Assert.True(batch.IsFullyReceived);
    }

    // ─── PurchaseOrder ────────────────────────────────────────────

    [Theory]
    [InlineData(PurchaseOrderStatus.Draft, true)]
    [InlineData(PurchaseOrderStatus.Submitted, true)]
    [InlineData(PurchaseOrderStatus.PartiallyReceived, false)]
    [InlineData(PurchaseOrderStatus.Received, false)]
    [InlineData(PurchaseOrderStatus.Cancelled, false)]
    [InlineData(PurchaseOrderStatus.Returned, false)]
    public void PurchaseOrder_CanBeCancelled_ShouldMatchExpected(
        PurchaseOrderStatus status, bool expected)
    {
        var order = new PurchaseOrder { Status = status };

        Assert.Equal(expected, order.CanBeCancelled());
    }

    // ─── StockTransfer ────────────────────────────────────────────

    [Theory]
    [InlineData(StockTransferStatus.Pending, true)]
    [InlineData(StockTransferStatus.Picked, true)]
    [InlineData(StockTransferStatus.InTransit, false)]
    [InlineData(StockTransferStatus.Received, false)]
    [InlineData(StockTransferStatus.Cancelled, false)]
    [InlineData(StockTransferStatus.Failed, false)]
    public void StockTransfer_CanBeCancelled_ShouldMatchExpected(
        StockTransferStatus status, bool expected)
    {
        var transfer = new StockTransfer { Status = status };

        Assert.Equal(expected, transfer.CanBeCancelled());
    }
}
