using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HttpOverrides;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RestaurantDelivery.Api.Authorization;
using RestaurantDelivery.Api.Configuration;
using RestaurantDelivery.Api.Hubs;
using RestaurantDelivery.Api.Services;
using RestaurantDelivery.Api.Services.Loyalty;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Enums;
using RestaurantDelivery.Core.Interfaces;
using RestaurantDelivery.Infrastructure.Data;
using RestaurantDelivery.Infrastructure.Data.Seed;
using RestaurantDelivery.Infrastructure.ExternalServices.Dgtera;
using RestaurantDelivery.Infrastructure.ExternalServices.GreenApi;
using RestaurantDelivery.Infrastructure.Repositories;
using RestaurantDelivery.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

const string AngularClientCorsPolicy = "AngularClient";

// :4200 is the normal dev server; :4300 serves a production build for PWA/service
// worker testing (ng serve doesn't run the service worker at all). The deployed
// frontend's origin comes from config (Cors__AllowedOrigins__0 on Railway) rather
// than being hardcoded, since it isn't known until the frontend's domain exists.
var corsOrigins = new List<string> { "http://localhost:4200", "http://localhost:4300" };
corsOrigins.AddRange(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? []);

builder.Services.AddCors(options =>
{
    options.AddPolicy(AngularClientCorsPolicy, policy =>
    {
        // AllowCredentials is required for the SignalR hub's cross-origin negotiation
        // (the dev server at :4200/:4300 is a different origin from the API) - safe
        // alongside WithOrigins, which is already explicit (never AllowAnyOrigin, which
        // AllowCredentials can't be combined with).
        policy.WithOrigins(corsOrigins.ToArray())
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services
    .AddIdentityCore<AppUser>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        options.Events = new JwtBearerEvents
        {
            // Browsers can't attach an Authorization header to a WebSocket upgrade
            // request, so the SignalR client sends the token as a query string param
            // instead (see the Angular loyalty-realtime service's accessTokenFactory) -
            // only honored under /hubs, never for regular API routes.
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("CaptainOnly", policy => policy.RequireRole("CaptainOrder"));
    // Order visibility/status-update is shared: admins manage the full order lifecycle,
    // captains (delivery drivers) only need to see orders and accept/complete deliveries.
    // Backed by a custom requirement (not RequireRole) so a restricted Admin custom Role
    // without the Orders module can be denied here too, while CaptainOrder always passes
    // regardless - see OrdersAccessAuthorizationHandler.
    options.AddPolicy("OrdersAccess", policy => policy.Requirements.Add(new OrdersAccessRequirement()));

    options.AddPolicy("Module.Orders", policy => policy.Requirements.Add(new PermissionRequirement(AdminModules.Orders)));
    options.AddPolicy("Module.MenuItems", policy => policy.Requirements.Add(new PermissionRequirement(AdminModules.MenuItems)));
    options.AddPolicy("Module.Settings", policy => policy.Requirements.Add(new PermissionRequirement(AdminModules.Settings)));
    options.AddPolicy("Module.Staff", policy => policy.Requirements.Add(new PermissionRequirement(AdminModules.Staff)));
    options.AddPolicy("Module.Customers", policy => policy.Requirements.Add(new PermissionRequirement(AdminModules.Customers)));
    options.AddPolicy("Module.Crm", policy => policy.Requirements.Add(new PermissionRequirement(AdminModules.Crm)));
    options.AddPolicy("Module.Campaigns", policy => policy.Requirements.Add(new PermissionRequirement(AdminModules.Campaigns)));
    options.AddPolicy("Module.Scanner", policy => policy.Requirements.Add(new PermissionRequirement(AdminModules.Scanner)));
    options.AddPolicy("Module.PromoCodes", policy => policy.Requirements.Add(new PermissionRequirement(AdminModules.PromoCodes)));
    options.AddPolicy("Module.Reviews", policy => policy.Requirements.Add(new PermissionRequirement(AdminModules.Reviews)));

    // The merged Customer Insights dashboard (formerly separate CRM/Customers pages) -
    // passes for either pre-existing module so no already-configured role loses access.
    options.AddPolicy(
        "Module.CustomerInsights",
        policy => policy.Requirements.Add(new AnyModuleRequirement(AdminModules.Crm, AdminModules.Customers)));

    // Granular sub-permission policies, ready for any future endpoint that maps onto
    // exactly one of these - see GranularPermissionAuthorizationHandler's doc comment for
    // why none of today's Orders/Settings endpoints are gated by these yet.
    options.AddPolicy("Permission.Orders.Create", policy => policy.Requirements.Add(new GranularPermissionRequirement("Orders.Create")));
    options.AddPolicy("Permission.Orders.AllOrders", policy => policy.Requirements.Add(new GranularPermissionRequirement("Orders.AllOrders")));
    options.AddPolicy("Permission.Orders.ActiveStatus", policy => policy.Requirements.Add(new GranularPermissionRequirement("Orders.ActiveStatus")));
    options.AddPolicy("Permission.Orders.Reports", policy => policy.Requirements.Add(new GranularPermissionRequirement("Orders.Reports")));
    options.AddPolicy("Permission.Settings.Branding", policy => policy.Requirements.Add(new GranularPermissionRequirement("Settings.Branding")));
    options.AddPolicy("Permission.Settings.Contact", policy => policy.Requirements.Add(new GranularPermissionRequirement("Settings.Contact")));
    options.AddPolicy("Permission.Settings.Checkout", policy => policy.Requirements.Add(new GranularPermissionRequirement("Settings.Checkout")));
    options.AddPolicy("Permission.Settings.Payment", policy => policy.Requirements.Add(new GranularPermissionRequirement("Settings.Payment")));
});

builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, OrdersAccessAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, AnyModuleAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, GranularPermissionAuthorizationHandler>();

builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddScoped<IMenuItemRepository, MenuItemRepository>();
builder.Services.AddScoped<IMenuItemService, MenuItemService>();
builder.Services.AddScoped<IBulkMenuItemImportService, BulkMenuItemImportService>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IBulkOrderImportService, BulkOrderImportService>();

builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<IFileUploadService, FileUploadService>();

builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<ICategoryService, CategoryService>();

builder.Services.AddScoped<ISubCategoryRepository, SubCategoryRepository>();
builder.Services.AddScoped<ISubCategoryService, SubCategoryService>();

builder.Services.AddScoped<IAddOnRepository, AddOnRepository>();
builder.Services.AddScoped<IAddOnService, AddOnService>();

builder.Services.AddScoped<ICustomerService, CustomerService>();

builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<IRoleService, RoleService>();

builder.Services.Configure<AppleWalletSettings>(builder.Configuration.GetSection(AppleWalletSettings.SectionName));
builder.Services.Configure<GoogleWalletSettings>(builder.Configuration.GetSection(GoogleWalletSettings.SectionName));
builder.Services.AddScoped<ILoyaltyService, LoyaltyService>();

builder.Services.AddScoped<ITierRepository, TierRepository>();
builder.Services.AddScoped<ITierService, TierService>();
builder.Services.AddScoped<IApplePassBuilder, ApplePassBuilder>();
builder.Services.AddScoped<IApplePassKitService, ApplePassKitService>();
builder.Services.AddSingleton<IWalletAuthTokenService, WalletAuthTokenService>();
builder.Services.AddSingleton<IGoogleWalletClientProvider, GoogleWalletClientProvider>();
builder.Services.AddScoped<IGoogleWalletService, GoogleWalletService>();
builder.Services.AddScoped<ICampaignService, CampaignService>();

builder.Services.AddScoped<IPromoCodeRepository, PromoCodeRepository>();
builder.Services.AddScoped<IPromoCodeService, PromoCodeService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();

builder.Services.AddSignalR();
builder.Services.AddSingleton<ILoyaltyRealtimeNotifier, LoyaltyRealtimeNotifier>();
builder.Services.AddSingleton<IOrderRealtimeNotifier, OrderRealtimeNotifier>();

builder.Services.Configure<DgteraOptions>(builder.Configuration.GetSection("Dgtera"));
builder.Services.AddHttpClient<IDgteraClient, DgteraClient>();
builder.Services.AddScoped<IDgteraSyncService, DgteraSyncService>();

builder.Services.Configure<GreenApiOptions>(builder.Configuration.GetSection("GreenApi"));
builder.Services.AddHttpClient<IWhatsAppNotificationService, WhatsAppNotificationService>();

var vapidSection = builder.Configuration.GetSection("Vapid");
var vapidPublicKey = vapidSection["PublicKey"] ?? throw new InvalidOperationException("Vapid:PublicKey is not configured.");
var vapidPrivateKey = vapidSection["PrivateKey"] ?? throw new InvalidOperationException("Vapid:PrivateKey is not configured.");
var vapidSubject = vapidSection["Subject"] ?? throw new InvalidOperationException("Vapid:Subject is not configured.");

// PushServiceClient wraps HttpClient and is safe to share as a singleton — registering it
// per-request would risk socket exhaustion under load, same reasoning as IHttpClientFactory.
builder.Services.AddSingleton(new PushServiceClient
{
    DefaultAuthentication = new VapidAuthentication(vapidPublicKey, vapidPrivateKey) { Subject = vapidSubject }
});

builder.Services.AddScoped<IWebPushSubscriptionRepository, WebPushSubscriptionRepository>();
builder.Services.AddScoped<IPushNotificationService, PushNotificationService>();

var app = builder.Build();

// Must run before anything that reads Request.Scheme (UploadImage's absolute-URL
// building, UseHttpsRedirection, etc.) - Railway terminates TLS at its edge and
// forwards plain HTTP to the container, so without this, Request.Scheme always reports
// "http" even for a client that connected over https, which is exactly why the
// branding logo and menu-item photo URLs got stored as http:// (triggering mixed
// content warnings) despite being served over a real https connection. KnownProxies/
// KnownNetworks are cleared because Railway's edge IP isn't a fixed, predictable
// address to allowlist - safe here because the container has no other inbound path
// except through that edge, so nothing else could spoof this header directly to Kestrel.
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedHeadersOptions.KnownIPNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Railway (and most reverse proxies) terminate TLS at the edge and forward plain
    // HTTP to the container, with no request ever arriving as HTTP externally - so in
    // Production this middleware would just redirect every request in an infinite loop.
    app.UseHttpsRedirection();
}

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await context.Database.MigrateAsync();
    await DataSeeder.SeedAsync(scope.ServiceProvider);
}

app.UseStaticFiles();

app.UseCors(AngularClientCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<LoyaltyHub>("/hubs/loyalty");
app.MapHub<OrderHub>("/hubs/orders");

app.Run();
