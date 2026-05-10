using System;
using System.Collections.Generic;
using System.Text;

namespace InventorySystem.Application.DTOs.Reporting
{
    public class SalesReportFilter
    {
        public Guid? WarehouseId { get; set; }
        public Guid? SupplierId { get; set; }
        public Guid? ProductCategoryId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }
}
