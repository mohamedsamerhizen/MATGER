using System.Text;
using System.Threading.RateLimiting;
using MATGER.Api.Authentication;
using MATGER.Api.Data;
using MATGER.Api.Identity;
using MATGER.Api.Interfaces;
using MATGER.Api.Middleware;
using MATGER.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            path: "logs/matger-.log",
            rollingInterval: RollingInterval.Day);
});

if (!builder.Environment.IsEnvironment("Testing"))
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        options.UseSqlServer(connectionString);
    });
}

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;

        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddDataProtection();

builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection(JwtSettings.SectionName));

builder.Services.Configure<DemoSeedOptions>(
    builder.Configuration.GetSection(DemoSeedOptions.SectionName));

var jwtSettings = builder.Configuration
    .GetSection(JwtSettings.SectionName)
    .Get<JwtSettings>()
    ?? throw new InvalidOperationException("JWT settings are not configured.");

if (string.IsNullOrWhiteSpace(jwtSettings.SecretKey))
{
    throw new InvalidOperationException("JWT secret key is not configured.");
}

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IAdminReportingService, AdminReportingService>();
builder.Services.AddScoped<ICouponService, CouponService>();
builder.Services.AddScoped<IInventoryMovementService, InventoryMovementService>();
builder.Services.AddScoped<IInventoryIntelligenceService, InventoryIntelligenceService>();
builder.Services.AddScoped<IRiskSignalService, RiskSignalService>();
builder.Services.AddScoped<ILoyaltyService, LoyaltyService>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddScoped<ICheckoutConsistencyService, CheckoutConsistencyService>();
builder.Services.AddScoped<IOrderFulfillmentService, OrderFulfillmentService>();
builder.Services.AddScoped<IReturnService, ReturnService>();
builder.Services.AddScoped<IRefundService, RefundService>();
builder.Services.AddScoped<IWishlistService, WishlistService>();
builder.Services.AddScoped<IProductReviewService, ProductReviewService>();
builder.Services.AddScoped<DemoDataSeeder>();

builder.Services.AddHostedService<ExpiredInventoryReservationService>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, cancellationToken) =>
    {
        var httpContext = context.HttpContext;

        if (httpContext.Response.HasStarted)
        {
            return;
        }

        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        await httpContext.Response.WriteAsJsonAsync(
            new
            {
                StatusCode = StatusCodes.Status429TooManyRequests,
                Message = "Too many authentication requests. Please try again later.",
                TraceId = httpContext.TraceIdentifier
            },
            cancellationToken);
    };

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        if (!IsRateLimitedAuthPath(httpContext.Request.Path))
        {
            return RateLimitPartition.GetNoLimiter("not-rate-limited");
        }

        var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown-ip";
        var partitionKey = $"auth:{remoteIp}";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 5,
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                Window = TimeSpan.FromMinutes(1)
            });
    });
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,

            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),

            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        AuthorizationPolicies.AdminOnly,
        policy => policy.RequireRole(ApplicationRoles.Admin));

    options.AddPolicy(
        AuthorizationPolicies.InventoryManagerOnly,
        policy => policy.RequireRole(
            ApplicationRoles.Admin,
            ApplicationRoles.InventoryManager));

    options.AddPolicy(
        AuthorizationPolicies.OrderManagerOnly,
        policy => policy.RequireRole(
            ApplicationRoles.Admin,
            ApplicationRoles.OrderManager));

    options.AddPolicy(
        AuthorizationPolicies.CustomerOnly,
        policy => policy.RequireRole(ApplicationRoles.Customer));
});

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "MATGER API",
        Version = "v1",
        Description = "Advanced E-commerce Backend Engine"
    });

    options.TagActionsBy(apiDescription =>
    [
        ResolveSwaggerTag(apiDescription)
    ]);

    options.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "JWT Authorization header using the Bearer scheme. Paste the token only; Swagger will send the Bearer prefix.",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("bearer", document)] = []
    });
});

builder.Services.AddHealthChecks();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await IdentitySeeder.SeedRolesAsync(scope.ServiceProvider);

    if (app.Environment.IsDevelopment())
    {
        await DevelopmentAdminSeeder.SeedAsync(scope.ServiceProvider);

        var demoDataSeeder = scope.ServiceProvider.GetRequiredService<DemoDataSeeder>();
        await demoDataSeeder.SeedAsync();
    }
}

app.UseMiddleware<RequestTracingMiddleware>();

app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}

app.UseRateLimiter();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health");

await app.RunAsync();

static bool IsRateLimitedAuthPath(PathString path)
{
    return path.StartsWithSegments(new PathString("/api/auth/login"), StringComparison.OrdinalIgnoreCase) ||
           path.StartsWithSegments(new PathString("/api/auth/register"), StringComparison.OrdinalIgnoreCase) ||
           path.StartsWithSegments(new PathString("/api/auth/refresh"), StringComparison.OrdinalIgnoreCase);
}

static string ResolveSwaggerTag(ApiDescription apiDescription)
{
    var controller = apiDescription.ActionDescriptor.RouteValues.TryGetValue("controller", out var controllerName)
        ? controllerName
        : null;

    return controller switch
    {
        "Auth" => "01 - Authentication",
        "Products" or "ProductVariants" or "Categories" or "Brands" or "ProductReviews" => "02 - Catalog",
        "Cart" or "Loyalty" or "Wallet" or "Wishlist" => "03 - Customer Shopping",
        "Checkout" => "04 - Checkout",
        "Orders" or "Returns" or "Refunds" => "05 - Orders, Returns & Refunds",
        "Inventory" or "InventoryIntelligence" => "06 - Inventory",
        "Coupons" or "ShippingMethods" => "07 - Promotions & Shipping",
        "AdminDashboard" or "AdminInventoryPlanning" or "AdminCustomers" or "AdminCustomerWallets" or "AdminLoyalty" or "AuditLogs" or "CheckoutConsistency" or "ProductReviewModeration" or "Fulfillment" or "RiskSignals" or "StockAdjustmentRequests" => "08 - Admin",
        "CommerceOperations" => "09 - Commerce Operations",
        "DemoData" => "10 - Demo Data",
        _ => controller ?? "General"
    };
}

public partial class Program;
