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

            // ✅ StockBatch relationships - منع الـ Cascade للحفاظ على التتبع
            modelBuilder.Entity<StockBatch>()
                .HasOne(sb => sb.Supplier)
                .WithMany()
                .HasForeignKey(sb => sb.SupplierId)
                .OnDelete(DeleteBehavior.NoAction);  // ✅ لا تحذف الشحنات عند حذف المورد

            modelBuilder.Entity<StockBatch>()
                .HasOne(sb => sb.Warehouse)
                .WithMany()
                .HasForeignKey(sb => sb.WarehouseId)
                .OnDelete(DeleteBehavior.NoAction);  // ✅ لا تحذف الشحنات عند حذف المستودع

            modelBuilder.Entity<StockBatch>()
                .HasOne(sb => sb.PurchaseOrderItem)
                .WithMany()
                .HasForeignKey(sb => sb.PurchaseOrderItemId)
                .OnDelete(DeleteBehavior.NoAction);  // ✅ لا تحذف الشحنات عند حذف عنصر الطلب

            // ✅ StockTransfer relationships
            modelBuilder.Entity<StockTransfer>()
                .HasOne(st => st.FromWarehouse)
                .WithMany()
                .HasForeignKey(st => st.FromWarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StockTransfer>()
                .HasOne(st => st.ToWarehouse)
                .WithMany()
                .HasForeignKey(st => st.ToWarehouseId)
                .OnDelete(DeleteBehavior.Restrict);

            // ✅ SaleItemBatchAllocation relationships
            modelBuilder.Entity<SaleItemBatchAllocation>()
                .HasOne(a => a.SaleItem)
                .WithMany(i => i.BatchAllocations)
                .HasForeignKey(a => a.SaleItemId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SaleItemBatchAllocation>()
                .HasOne(a => a.StockBatch)
                .WithMany()
                .HasForeignKey(a => a.StockBatchId)
                .OnDelete(DeleteBehavior.Restrict);

            // ✅ Indexes - بسيطة وفعالة
            modelBuilder.Entity<StockBatch>()
                .HasIndex(x => new { x.ProductId, x.WarehouseId, x.SupplierId });

            modelBuilder.Entity<StockBatch>()
                .HasIndex(x => x.PurchaseOrderItemId);

            modelBuilder.Entity<Sale>()
                .HasIndex(x => new { x.WarehouseId, x.SaleDate });

            modelBuilder.Entity<SaleItemBatchAllocation>()
                .HasIndex(x => new { x.SaleItemId, x.StockBatchId });

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