using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RestaurantDelivery.Core.DTOs.Dgtera;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Infrastructure.ExternalServices.Dgtera;

// Talks to Dgtera's Odoo backend over Odoo's standard JSON-RPC external API
// (https://www.odoo.com/documentation/latest/developer/reference/external_api.html).
// This is a starting point, not a verified integration: it targets the "pos.order" /
// "pos.order.line" models, which is the common shape for a point-of-sale setup like
// Dgtera's, but the exact model and field names can vary per Odoo install/customization.
// Confirm both against the real instance (e.g. via Settings > Technical > Database
// Structure > Models, with the Developer Mode enabled) before relying on this in
// production - the //  TODO comments below flag the specific spots most likely to need
// adjustment.
public class DgteraClient : IDgteraClient
{
    private readonly HttpClient _httpClient;
    private readonly DgteraOptions _options;
    private readonly ILogger<DgteraClient> _logger;

    public DgteraClient(HttpClient httpClient, IOptions<DgteraOptions> options, ILogger<DgteraClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<List<DgteraOrderDto>> GetRecentOrdersAsync(CancellationToken cancellationToken = default)
    {
        var uid = await AuthenticateAsync(cancellationToken);

        // TODO: "pos.order" is Odoo's Point of Sale order model. If Dgtera is configured
        // to sync through regular sales orders instead, use "sale.order" here (and adjust
        // the field names in the search_read calls below - e.g. "amount_total" and
        // "date_order" exist on both, but "lines" is "order_line" on sale.order).
        const string orderModel = "pos.order";
        var orderFields = new[] { "name", "partner_id", "amount_total", "date_order", "state", "lines" };

        // TODO: an empty domain (no filter) fetches every order on every run - fine for a
        // manual "Sync Now" button on a small dataset, but for a POS with real order
        // volume, narrow this to a lookback window, e.g.:
        //   new object[] { new object[] { "date_order", ">=", DateTime.UtcNow.AddDays(-7).ToString("yyyy-MM-dd HH:mm:ss") } }
        var domain = Array.Empty<object>();

        var rawOrders = await ExecuteKwAsync(
            uid,
            orderModel,
            "search_read",
            new object[] { domain },
            new Dictionary<string, object> { ["fields"] = orderFields },
            cancellationToken);

        var orderDtos = new List<DgteraOrderDto>();
        var lineIdsByOrder = new Dictionary<string, List<int>>();

        foreach (var raw in rawOrders.EnumerateArray())
        {
            var externalId = raw.GetProperty("id").ToString();
            var dto = new DgteraOrderDto
            {
                ExternalId = externalId,
                ReferenceNumber = GetString(raw, "name"),
                CustomerName = GetMany2OneName(raw, "partner_id") ?? "Dgtera Customer",
                OrderDateUtc = GetOdooDateTimeUtc(raw, "date_order"),
                RawStatus = GetString(raw, "state") ?? string.Empty
            };
            orderDtos.Add(dto);

            lineIdsByOrder[externalId] = raw.TryGetProperty("lines", out var linesEl) && linesEl.ValueKind == JsonValueKind.Array
                ? linesEl.EnumerateArray().Select(e => e.GetInt32()).ToList()
                : new List<int>();
        }

        var allLineIds = lineIdsByOrder.Values.SelectMany(ids => ids).Distinct().ToList();
        if (allLineIds.Count == 0)
        {
            return orderDtos;
        }

        // TODO: "pos.order.line" mirrors "pos.order" above - use "sale.order.line" if
        // this Dgtera instance syncs through sale orders instead.
        var rawLines = await ExecuteKwAsync(
            uid,
            "pos.order.line",
            "read",
            new object[] { allLineIds },
            new Dictionary<string, object> { ["fields"] = new[] { "product_id", "qty", "price_unit" } },
            cancellationToken);

        var lineById = new Dictionary<int, JsonElement>();
        var productIds = new HashSet<int>();
        foreach (var line in rawLines.EnumerateArray())
        {
            lineById[line.GetProperty("id").GetInt32()] = line;
            var productId = GetMany2OneId(line, "product_id");
            if (productId.HasValue)
            {
                productIds.Add(productId.Value);
            }
        }

        // A product's category name comes back directly as categ_id: [id, "Category Name"]
        // on product.product/product.template - Odoo includes the display name for
        // many2one fields automatically, so no extra join is needed for that part.
        var productsById = new Dictionary<int, JsonElement>();
        if (productIds.Count > 0)
        {
            var rawProducts = await ExecuteKwAsync(
                uid,
                "product.product",
                "read",
                new object[] { productIds.ToList() },
                new Dictionary<string, object> { ["fields"] = new[] { "name", "categ_id" } },
                cancellationToken);

            foreach (var product in rawProducts.EnumerateArray())
            {
                productsById[product.GetProperty("id").GetInt32()] = product;
            }
        }

        foreach (var dto in orderDtos)
        {
            foreach (var lineId in lineIdsByOrder[dto.ExternalId])
            {
                if (!lineById.TryGetValue(lineId, out var line))
                {
                    continue;
                }

                var productId = GetMany2OneId(line, "product_id");
                var product = productId.HasValue && productsById.TryGetValue(productId.Value, out var p) ? p : (JsonElement?)null;

                dto.Lines.Add(new DgteraOrderLineDto
                {
                    ProductName = (product.HasValue ? GetString(product.Value, "name") : null)
                        ?? GetMany2OneName(line, "product_id")
                        ?? "Unknown item",
                    CategoryName = product.HasValue ? GetMany2OneName(product.Value, "categ_id") : null,
                    Quantity = (int)Math.Round(GetDouble(line, "qty") ?? 1),
                    UnitPrice = (decimal)(GetDouble(line, "price_unit") ?? 0)
                });
            }
        }

        return orderDtos;
    }

    private async Task<int> AuthenticateAsync(CancellationToken cancellationToken)
    {
        var payload = BuildRpcPayload("common", "authenticate", new object[]
        {
            _options.Database,
            _options.Username,
            _options.ApiKey,
            new { }
        });

        var result = await PostAsync(payload, cancellationToken);

        // Odoo returns `false` (not an error response) for bad credentials.
        if (result.ValueKind != JsonValueKind.Number)
        {
            throw new InvalidOperationException(
                "Dgtera authentication failed - check Dgtera:Database, Dgtera:Username, and Dgtera:ApiKey.");
        }

        return result.GetInt32();
    }

    private async Task<JsonElement> ExecuteKwAsync(
        int uid,
        string model,
        string method,
        object[] args,
        Dictionary<string, object>? kwargs,
        CancellationToken cancellationToken)
    {
        var payload = BuildRpcPayload("object", "execute_kw", new object[]
        {
            _options.Database,
            uid,
            _options.ApiKey,
            model,
            method,
            args,
            kwargs ?? new Dictionary<string, object>()
        });

        return await PostAsync(payload, cancellationToken);
    }

    private object BuildRpcPayload(string service, string method, object[] args) => new
    {
        jsonrpc = "2.0",
        method = "call",
        @params = new { service, method, args },
        id = Random.Shared.Next()
    };

    private async Task<JsonElement> PostAsync(object payload, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync($"{_options.BaseUrl}/jsonrpc", payload, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var document = await JsonSerializer.DeserializeAsync<JsonDocument>(stream, cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Dgtera returned an empty response.");

        var root = document.RootElement;
        if (root.TryGetProperty("error", out var error))
        {
            // The full error (often a multi-KB Python traceback in "data.debug") goes to
            // the server log only - callers only ever see the short, human-readable
            // "data.message" Odoo provides (e.g. "FATAL: database ... does not exist"),
            // so a sync failure surfaces as one readable line in the admin's toast
            // instead of a wall of stack trace.
            _logger.LogError("Dgtera JSON-RPC error: {Error}", error.ToString());

            var shortMessage = error.TryGetProperty("data", out var data) && data.TryGetProperty("message", out var dataMessage)
                ? dataMessage.GetString()
                : error.TryGetProperty("message", out var topMessage)
                    ? topMessage.GetString()
                    : null;

            throw new InvalidOperationException(shortMessage?.Trim() ?? "Dgtera returned an unspecified error.");
        }

        return root.GetProperty("result").Clone();
    }

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static double? GetDouble(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : null;

    // Odoo many2one fields come back as a two-element [id, "Display Name"] array, or
    // `false` when unset - never a bare id or a nested object.
    private static int? GetMany2OneId(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array && value.GetArrayLength() > 0
            ? value[0].GetInt32()
            : null;

    private static string? GetMany2OneName(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array && value.GetArrayLength() > 1
            ? value[1].GetString()
            : null;

    private static DateTime GetOdooDateTimeUtc(JsonElement element, string property)
    {
        // Odoo returns naive datetimes in server time (UTC by default) as
        // "yyyy-MM-dd HH:mm:ss", not ISO 8601.
        var raw = GetString(element, property);
        return raw is not null && DateTime.TryParse(raw, out var parsed)
            ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
            : DateTime.UtcNow;
    }
}
