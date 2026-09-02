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
using RestaurantDelivery.Api.Services;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Enums;
using RestaurantDelivery.Core.Interfaces;
using RestaurantDelivery.Infrastructure.Data;
using RestaurantDelivery.Infrastructure.Data.Seed;
using RestaurantDelivery.Infrastructure.ExternalServices.Dgtera;
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
        policy.WithOrigins(corsOrigins.ToArray())
            .AllowAnyHeader()
            .AllowAnyMethod();
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
});

builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, OrdersAccessAuthorizationHandler>();

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

builder.Services.AddScoped<IAddOnRepository, AddOnRepository>();
builder.Services.AddScoped<IAddOnService, AddOnService>();

builder.Services.AddScoped<ICustomerService, CustomerService>();

builder.Services.AddScoped<IRoleRepository, RoleRepository>();
builder.Services.AddScoped<IRoleService, RoleService>();

builder.Services.Configure<DgteraOptions>(builder.Configuration.GetSection("Dgtera"));
builder.Services.AddHttpClient<IDgteraClient, DgteraClient>();
builder.Services.AddScoped<IDgteraSyncService, DgteraSyncService>();

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

app.Run();
