using InventorySystem.Application.Interfaces;
using InventorySystem.Infrastructure.Services;

namespace InventorySystem.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IPurchaseService, PurchaseService>();
        services.AddScoped<ISalesService, SalesService>();
        services.AddScoped<IStockTransferService, StockTransferService>();

        return services;
    }
}
