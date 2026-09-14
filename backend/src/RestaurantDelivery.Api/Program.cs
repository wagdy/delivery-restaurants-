using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using RestaurantDelivery.Api.Infrastructure;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
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
using RestaurantDelivery.Infrastructure.ExternalServices.OpenWa;
using RestaurantDelivery.Infrastructure.Repositories;
using RestaurantDelivery.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Structured logging. Railway parses single-line JSON from stdout into searchable fields,
// so Production writes compact JSON while local development keeps the readable console
// format - the same events either way, only the rendering differs.
//
// EF Core's command logging is pinned to Warning: at Information it prints every SQL
// statement with its parameters, which buried real events under pages of query text and
// put customer data (phone numbers, addresses) into the log stream. Raise it deliberately
// and temporarily when debugging a query, rather than leaving it on by default.
builder.Services.AddSerilog((services, config) =>
{
    config
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
        .Enrich.FromLogContext();

    if (builder.Environment.IsProduction())
    {
        config.WriteTo.Console(new CompactJsonFormatter());
    }
    else
    {
        config.WriteTo.Console();
    }
});

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Two endpoints with different jobs, mapped further down:
//
//   /health        liveness  - is this process running? No dependency checks, so a
//                              database blip never makes Railway kill a healthy container
//                              that would have recovered on its own.
//   /health/ready  readiness - can it actually serve? Opens a real connection to Postgres,
//                              so a container that boots but can't reach its database is
//                              reported unhealthy instead of quietly accepting orders it
//                              cannot store.
//
// Railway's healthcheck path should point at /health/ready: a failing deploy then rolls
// back on its own instead of replacing a working release with a broken one.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>(
        name: "database",
        tags: ["ready"]);

// Global exception handling. AddProblemDetails supplies the RFC 7807 responses for
// framework-generated failures (404s, 415s); GlobalExceptionHandler covers anything a
// controller throws.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

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

        // Brute-force guard for the password login path - AuthService.LoginAsync records
        // each failure via UserManager.AccessFailedAsync and refuses a locked-out account,
        // mirroring what the OTP reset flow already did with its own MaxOtpAttempts cap.
        // 15 minutes is short enough that a staff member who fat-fingers their password at
        // the start of a shift isn't locked out for the rest of it, and long enough that an
        // online guessing attack is reduced to a few hundred attempts a day.
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");

// HMAC-SHA256 derives its strength from the key's length, and a short key silently
// weakens every token the API issues - there's no runtime symptom, so the only place this
// can be caught is here. 32 bytes matches the algorithm's own output size, which is the
// usual floor for HS256. Failing at startup means a misconfigured deploy never serves
// traffic at all, rather than serving forgeable tokens.
if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key must be at least 32 bytes (256 bits) for HMAC-SHA256. " +
        $"The configured key is {Encoding.UTF8.GetByteCount(jwtKey)} bytes.");
}

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

// Per-IP rate limiting for the endpoints reachable without a token (see
// RateLimitPolicies for why only those). Partitioning on the client IP relies on
// UseForwardedHeaders running first in the pipeline below - without it every request
// would look like it came from Railway's edge and share a single bucket.
//
// Each of these costs something real when abused: a WhatsApp message, a kitchen ticket,
// a row in the customers table, or a guess at a password or promo code. The windows below
// are sized so a normal customer or staff member never notices them.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Without this the client just gets a bare 429 with no idea when to try again - the
    // Angular error interceptor can show "try again in N seconds" instead of a generic
    // failure. Only set for fixed windows, which are the only kind used here.
    options.OnRejected = async (context, ct) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(NumberFormatInfo.InvariantInfo);
        }

        await context.HttpContext.Response.WriteAsJsonAsync(
            new { errors = new[] { "Too many requests. Please wait a moment and try again." } }, ct);
    };

    // These four are only ever reached by a caller who has no token yet, so every request
    // is limited.
    AddFixedWindowPerIp(options, RateLimitPolicies.Login, permit: 20, windowMinutes: 5);
    AddFixedWindowPerIp(options, RateLimitPolicies.Register, permit: 5, windowMinutes: 15);
    AddFixedWindowPerIp(options, RateLimitPolicies.OtpRequest, permit: 3, windowMinutes: 15);
    AddFixedWindowPerIp(options, RateLimitPolicies.OtpVerify, permit: 10, windowMinutes: 15);

    // These two are anonymous for guest checkout but are also used by signed-in staff -
    // the admin "Create Order" screen takes phone-in orders through the very same
    // endpoint, and validates promo codes against the same one. A flat per-IP budget
    // would throttle the restaurant's own till during exactly the dinner rush it's meant
    // to protect, since all its terminals share one connection. Authenticated callers are
    // therefore exempt: they already had to sign in, and an abusive account can be
    // disabled, which is not true of an anonymous flood.
    AddFixedWindowPerIpForAnonymous(options, RateLimitPolicies.PromoValidate, permit: 20, windowMinutes: 5);
    AddFixedWindowPerIpForAnonymous(options, RateLimitPolicies.OrderCreate, permit: 10, windowMinutes: 10);

    // Local functions rather than six near-identical lambdas - the only things that vary
    // between these policies are the budget and whether signed-in callers are exempt.
    static void AddFixedWindowPerIp(RateLimiterOptions options, string policyName, int permit, int windowMinutes) =>
        options.AddPolicy(policyName, httpContext => PerIpWindow(httpContext, permit, windowMinutes));

    static void AddFixedWindowPerIpForAnonymous(RateLimiterOptions options, string policyName, int permit, int windowMinutes) =>
        options.AddPolicy(policyName, httpContext => httpContext.User.Identity?.IsAuthenticated == true
            ? RateLimitPartition.GetNoLimiter("authenticated")
            : PerIpWindow(httpContext, permit, windowMinutes));

    static RateLimitPartition<string> PerIpWindow(HttpContext httpContext, int permit, int windowMinutes) =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ClientIpFor(httpContext),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permit,
                Window = TimeSpan.FromMinutes(windowMinutes),
                // No queueing: a caller over budget should be told so immediately, not
                // held open on a connection waiting for a slot.
                QueueLimit = 0
            });

    // The partition key every policy above buckets on.
    //
    // X-Real-IP, not Connection.RemoteIpAddress: Railway's edge documents X-Real-IP as
    // the client's address (https://docs.railway.com/networking/public-networking/specs-and-limits),
    // and it is the edge that sets it, so a client cannot forge it. RemoteIpAddress is
    // not usable here - it resolved to a per-connection internal peer address in
    // production, which gave every new TCP connection its own fresh budget and made the
    // limiter trivially bypassable by reconnecting. That was only visible against the
    // deployed environment: locally, where nothing sits in front of Kestrel, the same
    // code partitioned correctly on the loopback address.
    //
    // Falls back to RemoteIpAddress and then to a single shared bucket, so an
    // unattributable request is still counted rather than exempted.
    static string ClientIpFor(HttpContext httpContext)
    {
        var realIp = httpContext.Request.Headers["X-Real-IP"].ToString();
        if (!string.IsNullOrWhiteSpace(realIp))
        {
            return realIp;
        }

        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
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

// Singleton so the cache outlives the request that populated it - the whole point. The
// data behind it (menu, categories, settings) is read on nearly every request and written
// only from the admin screens, which invalidate their group explicitly on save.
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IReadThroughCache, ReadThroughCache>();

builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<IFileUploadService, FileUploadService>();
builder.Services.AddScoped<IImageBackfillService, ImageBackfillService>();

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
builder.Services.AddScoped<ISurveySectionService, SurveySectionService>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();

builder.Services.AddSignalR();
builder.Services.AddSingleton<ILoyaltyRealtimeNotifier, LoyaltyRealtimeNotifier>();
builder.Services.AddSingleton<IOrderRealtimeNotifier, OrderRealtimeNotifier>();

builder.Services.Configure<DgteraOptions>(builder.Configuration.GetSection("Dgtera"));
builder.Services.AddHttpClient<IDgteraClient, DgteraClient>(client =>
    {
        // The POS sync pulls a batch of orders in one JSON-RPC call, so it gets a longer
        // ceiling than the WhatsApp clients below - but a ceiling nonetheless, rather than
        // HttpClient's 100-second default.
        client.Timeout = TimeSpan.FromSeconds(60);
    })
    .AddStandardResilienceHandler(options =>
    {
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(60);
        // Must exceed AttemptTimeout or the handler rejects the configuration outright.
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
    });
builder.Services.AddScoped<IDgteraSyncService, DgteraSyncService>();

// WhatsApp:UseOpenWa is a plain config toggle (appsettings.json / WhatsApp__UseOpenWa on
// Railway) - not a runtime feature flag, since IWhatsAppNotificationService's implementation
// is fixed for the lifetime of the process once DI is built. Flip it and redeploy to switch
// providers; only ONE of the two branches below ever registers, so there's exactly one
// implementation behind IWhatsAppNotificationService at a time - registering both would leave
// whichever call happened to run second silently shadowing the first for every caller
// (AuthService, LoyaltyService, OrderService) with no error to signal the conflict.
// Whichever provider is active gets the same resilience treatment (see
// AddWhatsAppResilience below for why these numbers, and why it matters more here than
// anywhere else in the app).
if (builder.Configuration.GetValue<bool>("WhatsApp:UseOpenWa"))
{
    builder.Services.Configure<OpenWaSettings>(builder.Configuration.GetSection("OpenWa"));
    AddWhatsAppResilience(builder.Services.AddHttpClient<IWhatsAppNotificationService, OpenWaWhatsAppNotificationService>());
}
else
{
    builder.Services.Configure<GreenApiOptions>(builder.Configuration.GetSection("GreenApi"));
    AddWhatsAppResilience(builder.Services.AddHttpClient<IWhatsAppNotificationService, WhatsAppNotificationService>());
}

// These calls are fire-and-forget (see OrderService and LoyaltyService, which never await
// them), which is exactly why they need a hard ceiling: nothing downstream is waiting to
// time them out. On HttpClient's 100-second default, a degraded provider during a dinner
// rush meant every order left a request hanging for a minute and a half with nothing
// bounding how many piled up.
//
// The retry matters just as much in the other direction: a single transient 502 used to
// lose a kitchen's "new order" alert permanently, with the failure visible only as a line
// in a log nobody was reading.
static void AddWhatsAppResilience(IHttpClientBuilder clientBuilder) =>
    clientBuilder
        .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(30))
        .AddStandardResilienceHandler(options =>
        {
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
            options.Retry.MaxRetryAttempts = 3;
            // Sampling window must be at least double the attempt timeout; the breaker
            // then stops hammering a provider that is already down, so an outage costs one
            // failed call per half-open probe instead of one per notification.
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        });

// The WhatsApp broadcast queue/worker (new Promo Code / Campaign announcements to every
// registered customer) - registered regardless of which WhatsApp provider is active above,
// since SendBroadcastMessageAsync is implemented by both. WhatsAppBroadcastQueue is
// exposed under both its own concrete type (so the hosted service below can reach its
// ChannelReader, which isn't part of the public IWhatsAppBroadcastQueue contract) and the
// interface (so PromoCodeService/CampaignService only depend on the abstraction).
builder.Services.AddSingleton<WhatsAppBroadcastQueue>();
builder.Services.AddSingleton<IWhatsAppBroadcastQueue>(sp => sp.GetRequiredService<WhatsAppBroadcastQueue>());
builder.Services.AddHostedService<WhatsAppBroadcastBackgroundService>();

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

// One structured line per request (method, path, status, duration) in place of the pages
// of raw SQL that used to fill the logs. The health endpoints are dropped to Verbose so
// Railway's own probe polling every few seconds doesn't drown out real traffic.
//
// Sits OUTSIDE UseExceptionHandler below, which is what keeps each failure to a single
// stack trace: the handler catches the exception and turns it into a 500 before this
// middleware ever sees one, so this logs a one-line "responded 500" while the handler logs
// the stack. With the order reversed, the exception passes through here on its way out and
// both write the full trace - doubling the log volume of exactly the events worth reading.
app.UseSerilogRequestLogging(options =>
{
    options.GetLevel = (httpContext, elapsed, ex) =>
    {
        if (ex is not null || httpContext.Response.StatusCode >= 500)
        {
            return LogEventLevel.Error;
        }

        return httpContext.Request.Path.StartsWithSegments("/health")
            ? LogEventLevel.Verbose
            : LogEventLevel.Information;
    };

    // Attached to every request line so a failure reported by a customer (who only has the
    // trace id from the error response) can be found alongside the exception it caused.
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("TraceId", httpContext.TraceIdentifier);
        diagnosticContext.Set("ClientIp", httpContext.Request.Headers["X-Real-IP"].ToString());
    };
});

// Wraps everything downstream, so an exception from authentication or model binding is
// caught here too, not only one thrown inside a controller action.
app.UseExceptionHandler();

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

// Deliberately placed after UseAuthentication, not before: the OrderCreate and
// PromoValidate policies exempt signed-in staff, and HttpContext.User isn't populated
// until the authentication middleware has run - limiting earlier would make every caller
// look anonymous and throttle the restaurant's own till. Still after UseForwardedHeaders
// (so per-IP partitions see the real client address rather than Railway's edge) and after
// UseCors (so a rejected request keeps the CORS headers the browser needs to surface the
// 429 to the Angular client instead of reporting an opaque network error).
app.UseRateLimiter();

app.UseAuthorization();

app.MapControllers();
app.MapHub<LoyaltyHub>("/hubs/loyalty");
app.MapHub<OrderHub>("/hubs/orders");

// Liveness: no dependency checks at all (Predicate excludes every registered check), so
// this answers only "is the process up and able to respond". A database hiccup must not
// make the platform kill a container that would have recovered by itself.
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false
});

// Readiness: runs the checks tagged "ready", currently a real connection to Postgres.
// This is the one Railway's healthcheck should target - a container that starts but can't
// reach its database reports unhealthy rather than accepting orders it cannot store.
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),

    // The default body is the bare word "Healthy". This returns which check failed and
    // how long it took, so a failing deploy says what is actually wrong without needing
    // someone to open a shell inside the container.
    ResponseWriter = async (httpContext, report) =>
    {
        httpContext.Response.ContentType = "application/json";
        await httpContext.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                durationMs = entry.Value.Duration.TotalMilliseconds,
                // Both, because they populate in different failure modes: a database that
                // refuses the connection reports a Description with no exception, while one
                // that throws mid-query reports the exception. Exception text only, never
                // the stack - this endpoint is unauthenticated.
                description = entry.Value.Description,
                error = entry.Value.Exception?.Message
            })
        });
    }
});

app.Run();
