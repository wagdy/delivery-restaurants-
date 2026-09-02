using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using RestaurantDelivery.Core.DTOs.Categories;
using RestaurantDelivery.Core.DTOs.MenuItems;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Infrastructure.Services;

public class BulkMenuItemImportService : IBulkMenuItemImportService
{
    // Column order the template ships with and ImportMenuItemsAsync expects - keep
    // these in sync if either one changes.
    private static readonly string[] Headers = { "Item Name", "Description", "Price", "Category", "Available" };

    private static readonly HashSet<string> TruthyValues = new(StringComparer.OrdinalIgnoreCase) { "yes", "y", "true", "1" };
    private static readonly HashSet<string> FalsyValues = new(StringComparer.OrdinalIgnoreCase) { "no", "n", "false", "0" };

    private readonly IMenuItemRepository _menuItemRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ICategoryService _categoryService;
    private readonly ILogger<BulkMenuItemImportService> _logger;

    public BulkMenuItemImportService(
        IMenuItemRepository menuItemRepository,
        ICategoryRepository categoryRepository,
        ICategoryService categoryService,
        ILogger<BulkMenuItemImportService> logger)
    {
        _menuItemRepository = menuItemRepository;
        _categoryRepository = categoryRepository;
        _categoryService = categoryService;
        _logger = logger;
    }

    // Generous headroom past the two example rows so the dropdown still works for
    // however many items the admin pastes/types in beyond them.
    private const int TemplateDataRowCount = 500;

    public async Task<Stream> GenerateTemplate()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Menu Items");

        for (var i = 0; i < Headers.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = Headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(63, 81, 181);
            cell.Style.Font.FontColor = XLColor.White;
        }

        // Two example rows: one for a category that (probably) already exists, one left
        // blank to show that Category can be skipped and filled in later - both are
        // still valid because the Category dropdown below allows blanks, and the import
        // itself falls back an empty cell to "Uncategorized" rather than rejecting it.
        var sampleRows = new object[,]
        {
            { "Chicken Shawarma Wrap", "Grilled chicken, garlic sauce, pickles, flatbread.", 9.99, "Mains", "Yes" },
            { "Baklava", "Layered filo pastry with nuts and honey syrup.", 4.5, "", "Yes" }
        };

        for (var row = 0; row < sampleRows.GetLength(0); row++)
        {
            for (var col = 0; col < sampleRows.GetLength(1); col++)
            {
                sheet.Cell(row + 2, col + 1).Value = XLCellValue.FromObject(sampleRows[row, col]);
            }
        }

        sheet.Columns().AdjustToContents();

        await AddCategoryDropdownAsync(workbook, sheet, sampleRows.GetLength(0));

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    // Adds a hidden "Categories" sheet listing every category currently in the
    // database, then points the Menu Items sheet's Category column (D) at it as a
    // dropdown - existing names are one click away instead of retyped (and
    // typo-prone), while a name that isn't in the list yet (a genuinely new category)
    // or a blank cell are both still accepted, matching what ImportMenuItemsAsync
    // actually allows.
    private async Task<IXLWorksheet> AddCategoryDropdownAsync(XLWorkbook workbook, IXLWorksheet menuItemsSheet, int sampleRowCount)
    {
        var categories = await _categoryRepository.GetAllOrderedAsync();

        var categoriesSheet = workbook.Worksheets.Add("Categories");
        categoriesSheet.Cell(1, 1).Value = "Category Name";
        categoriesSheet.Cell(1, 1).Style.Font.Bold = true;

        for (var i = 0; i < categories.Count; i++)
        {
            categoriesSheet.Cell(i + 2, 1).Value = categories[i].Name;
        }

        categoriesSheet.Columns().AdjustToContents();
        // Keeps the picklist out of the way without deleting it - still fully visible
        // via Excel's own Sheet > Unhide if someone wants to check or edit the list.
        categoriesSheet.Visibility = XLWorksheetVisibility.Hidden;

        // Referencing at least one real row keeps the formula valid (and the dropdown
        // functional) even on a brand-new restaurant with zero categories yet.
        var lastCategoryRow = Math.Max(categories.Count, 1) + 1;
        var sourceRange = $"'{categoriesSheet.Name}'!$A$2:$A${lastCategoryRow}";

        var lastDataRow = Math.Max(sampleRowCount, 0) + 1 + TemplateDataRowCount;
        var categoryColumn = menuItemsSheet.Range($"D2:D{lastDataRow}");

        var validation = categoryColumn.CreateDataValidation();
        validation.List(sourceRange, inCellDropdown: true);
        validation.IgnoreBlanks = true;
        // Warns instead of blocking: a name that doesn't match anything in the list
        // yet is a legitimate new category (auto-created on import), not a mistake -
        // Stop would make that impossible to type directly into Excel.
        validation.ErrorStyle = XLErrorStyle.Information;
        validation.ErrorTitle = "Not an existing category";
        validation.ErrorMessage = "This will be created as a new category on import. Leave blank to use \"Uncategorized\" for now instead.";

        return categoriesSheet;
    }

    public async Task<BulkMenuItemImportResult> ImportMenuItemsAsync(Stream fileStream)
    {
        var result = new BulkMenuItemImportResult();

        using var workbook = new XLWorkbook(fileStream);
        var sheet = workbook.Worksheets.First();

        // Row 1 is the header row (see GenerateTemplate) - data starts at row 2.
        var dataRows = sheet.RowsUsed().Skip(1);

        foreach (var row in dataRows)
        {
            var rowNumber = row.RowNumber();
            var name = row.Cell(1).GetString().Trim();
            var description = row.Cell(2).GetString().Trim();
            var priceRaw = row.Cell(3).GetString().Trim();
            var categoryName = row.Cell(4).GetString().Trim();
            var availableRaw = row.Cell(5).GetString().Trim();

            // ClosedXML's RowsUsed() can include rows that only ever had formatting
            // applied - skip those silently rather than reporting them as invalid data.
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(priceRaw))
            {
                continue;
            }

            result.RowsProcessed++;

            if (string.IsNullOrWhiteSpace(name))
            {
                result.RowsSkipped++;
                result.Errors.Add($"Row {rowNumber}: Item Name is required.");
                continue;
            }

            if (!decimal.TryParse(priceRaw, out var price) || price <= 0)
            {
                result.RowsSkipped++;
                result.Errors.Add($"Row {rowNumber}: Price must be a positive number.");
                continue;
            }

            if (!TryParseAvailable(availableRaw, out var isAvailable))
            {
                result.RowsSkipped++;
                result.Errors.Add($"Row {rowNumber}: Available must be Yes/No (or blank for Yes).");
                continue;
            }

            try
            {
                var category = await ResolveCategoryAsync(categoryName);

                var existing = await _menuItemRepository.GetByNameAsync(name);
                if (existing is not null)
                {
                    existing.Description = string.IsNullOrWhiteSpace(description) ? existing.Description : description;
                    existing.Price = price;
                    existing.Category = category;
                    existing.IsAvailable = isAvailable;
                    _menuItemRepository.Update(existing);
                    result.ItemsUpdated++;
                }
                else
                {
                    await _menuItemRepository.AddAsync(new MenuItem
                    {
                        Name = name,
                        Description = string.IsNullOrWhiteSpace(description) ? null : description,
                        Price = price,
                        Category = category,
                        IsAvailable = isAvailable
                    });
                    result.ItemsCreated++;
                }

                // Saved per-row rather than once at the end: one malformed row shouldn't
                // roll back every item already imported earlier in the same file.
                await _menuItemRepository.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to import menu item \"{Name}\" from row {RowNumber}", name, rowNumber);
                result.RowsSkipped++;
                result.Errors.Add($"Row {rowNumber} (\"{name}\"): {ex.Message}");
            }
        }

        return result;
    }

    // Matches an existing Category by name, creating one (appended to the end of the
    // admin's configured display order) if nothing matches yet - a new menu item's
    // category doesn't need to already exist ahead of time. A blank cell (allowed by
    // the template's dropdown - see AddCategoryDropdownAsync) falls back to
    // "Uncategorized" rather than being rejected, so the admin can add the item now and
    // assign a real category later.
    private async Task<string> ResolveCategoryAsync(string categoryName)
    {
        var name = string.IsNullOrWhiteSpace(categoryName) ? "Uncategorized" : categoryName.Trim();

        var existing = await _categoryRepository.GetByNameAsync(name);
        if (existing is not null)
        {
            return existing.Name;
        }

        var created = await _categoryService.CreateAsync(new CategoryRequest { Name = name });
        if (!created.Succeeded)
        {
            throw new InvalidOperationException(created.Errors.FirstOrDefault() ?? "Failed to create category.");
        }

        return created.Data!.Name;
    }

    private static bool TryParseAvailable(string raw, out bool isAvailable)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            isAvailable = true;
            return true;
        }

        if (TruthyValues.Contains(raw))
        {
            isAvailable = true;
            return true;
        }

        if (FalsyValues.Contains(raw))
        {
            isAvailable = false;
            return true;
        }

        isAvailable = false;
        return false;
    }
}
