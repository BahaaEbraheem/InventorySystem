using InventorySystem.Domain.Common;
using InventorySystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Net.ServerSentEvents;
using System.Reflection.Emit;
using System.Text;

namespace InventorySystem.Infrastructure.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<Product> Products => Set<Product>();
        public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
        public DbSet<Supplier> Suppliers => Set<Supplier>();
        public DbSet<Warehouse> Warehouses => Set<Warehouse>();
        public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
        public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
        public DbSet<StockBatch> StockBatches => Set<StockBatch>();
        public DbSet<Sale> Sales => Set<Sale>();
        public DbSet<SaleItem> SaleItems => Set<SaleItem>();
        public DbSet<SaleItemBatchAllocation> SaleItemBatchAllocations => Set<SaleItemBatchAllocation>();
        public DbSet<StockTransfer> StockTransfers => Set<StockTransfer>();
        public DbSet<StockTransferItem> StockTransferItems => Set<StockTransferItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Soft Delete Filter
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(AuditableEntity).IsAssignableFrom(entityType.ClrType))
                {
                    modelBuilder.Entity(entityType.ClrType)
                        .HasQueryFilter(GetSoftDeleteFilter(entityType.ClrType));
                }
            }

            // Indexes
            modelBuilder.Entity<StockBatch>()
                .HasIndex(x => new { x.ProductId, x.WarehouseId, x.PurchaseDate });

            modelBuilder.Entity<Sale>()
    .HasIndex(x => x.SaleDate);

            modelBuilder.Entity<Sale>()
                .HasIndex(x => x.WarehouseId);

            modelBuilder.Entity<StockBatch>()
                .HasIndex(x => x.SupplierId);

            modelBuilder.Entity<Product>()
                .HasIndex(x => x.CategoryId);

        }

        private static LambdaExpression GetSoftDeleteFilter(Type type)
        {
            var param = Expression.Parameter(type, "e");
            var prop = Expression.Property(param, nameof(AuditableEntity.IsDeleted));
            var condition = Expression.Equal(prop, Expression.Constant(false));
            return Expression.Lambda(condition, param);
        }
    }
}