using HealthChecks.UI.Client;
using InventorySystem.Api.Extensions;
using InventorySystem.Api.Hubs;
using InventorySystem.Api.Services;
using InventorySystem.Application.Interfaces;
using InventorySystem.Infrastructure.Extensions;
using InventorySystem.Infrastructure.Persistence;
using InventorySystem.Infrastructure.Seed;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// 🔐 CORS Configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("AllowedOrigins")
            .Get<string[]>() ?? Array.Empty<string>();

        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// 🎮 Controllers & Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Inventory System API", Version = "v1" });
    // إضافة دعم للـ JWT إذا لزم
});

// 🗄️ Database with Retry Policy
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql
            .EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null)
            .CommandTimeout(30)
    )
);

// 📡 SignalR Configuration
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
});

// 🔧 Services Registration
builder.Services.AddApplicationServices();
builder.Services.AddScoped<INotificationService, NotificationService>();

// ❤️ Health Checks
builder.Services.AddHealthChecks()
    .AddSqlServer(  // ✅ الآن سيعمل بعد تثبيت الـ package
        connectionString: builder.Configuration.GetConnectionString("DefaultConnection")!,
        name: "sqlserver-primary",
        failureStatus: HealthStatus.Degraded,
        tags: new[] { "database", "sqlserver" });




// ⚡ Rate Limiting (Optional but Recommended)
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("api", limiter =>
    {
        limiter.PermitLimit = 100;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 10;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var app = builder.Build();

// 🌐 Middleware Pipeline - Order Matters!
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "API v1"));
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseGlobalExceptionHandler();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// 📡 SignalR Hub
app.MapHub<StockHub>("/hubs/stock");

// 🗺️ Controllers
app.MapControllers();

// ❤️ Health Endpoints
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = _ => true
});

// 🌱 Database Migration & Seeding (Development Only)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var db = services.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        if (!await db.Products.AnyAsync())
        {
            logger.LogInformation("🌱 Seeding initial data...");
            await DbSeeder.SeedAsync(db);
            logger.LogInformation("✅ Seeding completed");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ Database migration/seeding failed");
        throw; // Fail fast in development
    }
}

app.Run();