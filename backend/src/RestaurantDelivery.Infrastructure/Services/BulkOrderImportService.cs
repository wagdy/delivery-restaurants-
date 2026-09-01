using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using RestaurantDelivery.Core.DTOs.Orders;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Enums;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Infrastructure.Services;

public class BulkOrderImportService : IBulkOrderImportService
{
    // Column order the template ships with and ImportOrdersAsync expects - keep these
    // in sync if either one changes.
    private static readonly string[] Headers = { "Customer Name", "Phone", "Address", "Item Name", "Quantity", "Notes" };

    private readonly IOrderRepository _orderRepository;
    private readonly IMenuItemRepository _menuItemRepository;
    private readonly ILogger<BulkOrderImportService> _logger;

    public BulkOrderImportService(
        IOrderRepository orderRepository,
        IMenuItemRepository menuItemRepository,
        ILogger<BulkOrderImportService> logger)
    {
        _orderRepository = orderRepository;
        _menuItemRepository = menuItemRepository;
        _logger = logger;
    }

    public Stream GenerateTemplate()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Orders");

        for (var i = 0; i < Headers.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = Headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(63, 81, 181);
            cell.Style.Font.FontColor = XLColor.White;
        }

        // Two rows for the same customer demonstrate the one behavior that isn't obvious
        // from the column headers alone: rows sharing the same Customer Name + Phone +
        // Address become line items on a single order, not two separate orders.
        var sampleRows = new object[,]
        {
            { "Jane Doe", "555-0123", "123 Main Street, Apt 4", "Margherita Pizza", 2, "Ring doorbell twice" },
            { "Jane Doe", "555-0123", "123 Main Street, Apt 4", "Iced Tea", 1, "" }
        };

        for (var row = 0; row < sampleRows.GetLength(0); row++)
        {
            for (var col = 0; col < sampleRows.GetLength(1); col++)
            {
                sheet.Cell(row + 2, col + 1).Value = XLCellValue.FromObject(sampleRows[row, col]);
            }
        }

        sheet.Columns().AdjustToContents();

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    public async Task<BulkOrderImportResult> ImportOrdersAsync(Stream fileStream)
    {
        var result = new BulkOrderImportResult();

        using var workbook = new XLWorkbook(fileStream);
        var sheet = workbook.Worksheets.First();

        // Row 1 is the header row (see GenerateTemplate) - data starts at row 2.
        var dataRows = sheet.RowsUsed().Skip(1);

        // Rows are grouped by matching customer info into one Order with multiple
        // OrderItems, in a List (not a Dictionary) so the resulting orders come out in
        // the same order customers first appear in the sheet.
        var groups = new List<CustomerGroup>();
        var groupsByKey = new Dictionary<string, CustomerGroup>();

        foreach (var row in dataRows)
        {
            var rowNumber = row.RowNumber();
            var customerName = row.Cell(1).GetString().Trim();
            var phone = row.Cell(2).GetString().Trim();
            var address = row.Cell(3).GetString().Trim();
            var itemName = row.Cell(4).GetString().Trim();
            var quantityRaw = row.Cell(5).GetString().Trim();
            var notes = row.Cell(6).GetString().Trim();

            // ClosedXML's RowsUsed() can include rows that only ever had formatting
            // applied (e.g. a leftover styled row past the real data) - skip those
            // silently rather than reporting them as invalid data.
            if (string.IsNullOrWhiteSpace(customerName) && string.IsNullOrWhiteSpace(itemName))
            {
                continue;
            }

            result.RowsProcessed++;

            if (string.IsNullOrWhiteSpace(customerName) || string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(itemName))
            {
                result.RowsSkipped++;
                result.Errors.Add($"Row {rowNumber}: Customer Name, Address, and Item Name are required.");
                continue;
            }

            if (!int.TryParse(quantityRaw, out var quantity) || quantity <= 0)
            {
                result.RowsSkipped++;
                result.Errors.Add($"Row {rowNumber}: Quantity must be a positive whole number.");
                continue;
            }

            var menuItem = await _menuItemRepository.GetByNameAsync(itemName);
            if (menuItem is null)
            {
                result.RowsSkipped++;
                result.Errors.Add($"Row {rowNumber}: No menu item named \"{itemName}\" was found.");
                continue;
            }

            var key = $"{customerName.ToLowerInvariant()}|{phone.ToLowerInvariant()}|{address.ToLowerInvariant()}";
            if (!groupsByKey.TryGetValue(key, out var group))
            {
                group = new CustomerGroup(customerName, phone, address);
                groupsByKey[key] = group;
                groups.Add(group);
            }

            group.Lines.Add(new OrderLine(menuItem, quantity, notes));
        }

        foreach (var group in groups)
        {
            try
            {
                var order = new Order
                {
                    CustomerName = group.CustomerName,
                    CustomerPhone = string.IsNullOrWhiteSpace(group.Phone) ? "N/A" : group.Phone,
                    DeliveryAddress = group.Address,
                    Notes = group.Lines.Select(l => l.Notes).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)),
                    Status = OrderStatus.Pending,
                    OrderItems = group.Lines.Select(line => new OrderItem
                    {
                        MenuItemId = line.MenuItem.Id,
                        MenuItem = line.MenuItem,
                        Quantity = line.Quantity,
                        UnitPrice = line.MenuItem.Price
                    }).ToList()
                };
                order.TotalAmount = order.OrderItems.Sum(i => i.UnitPrice * i.Quantity);

                await _orderRepository.AddAsync(order);

                // Saved per-order, same reasoning as DgteraSyncService: one bad record
                // shouldn't roll back every other order already imported this run.
                await _orderRepository.SaveChangesAsync();
                result.OrdersCreated++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create order for {CustomerName} during bulk import", group.CustomerName);
                result.Errors.Add($"Order for {group.CustomerName}: {ex.Message}");
            }
        }

        return result;
    }

    private sealed class CustomerGroup
    {
        public CustomerGroup(string customerName, string phone, string address)
        {
            CustomerName = customerName;
            Phone = phone;
            Address = address;
        }

        public string CustomerName { get; }
        public string Phone { get; }
        public string Address { get; }
        public List<OrderLine> Lines { get; } = new();
    }

    private sealed record OrderLine(MenuItem MenuItem, int Quantity, string Notes);
}
