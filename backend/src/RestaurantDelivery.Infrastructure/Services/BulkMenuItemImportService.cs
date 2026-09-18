using ClosedXML.Excel;
using Microsoft.Extensions.Logging;
using RestaurantDelivery.Core.DTOs.Categories;
using RestaurantDelivery.Core.DTOs.MenuItems;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Interfaces;
using RestaurantDelivery.Infrastructure.Excel;

namespace RestaurantDelivery.Infrastructure.Services;

public class BulkMenuItemImportService : IBulkMenuItemImportService
{
    private static readonly HashSet<string> TruthyValues = new(StringComparer.OrdinalIgnoreCase) { "yes", "y", "true", "1" };
    private static readonly HashSet<string> FalsyValues = new(StringComparer.OrdinalIgnoreCase) { "no", "n", "false", "0" };

    // Headers this importer understands, old spellings included. The template used to
    // ship "Item Name"/"Category"; a sheet an admin downloaded before the columns
    // changed is still sitting in somebody's Downloads folder, and it has to keep
    // importing correctly rather than being read a column out of step.
    private static readonly string[] NameEnglishAliases = { MenuTemplateWorkbook.NameEnglishHeader, "Item Name", "Name" };
    private static readonly string[] NameArabicAliases = { MenuTemplateWorkbook.NameArabicHeader, "Arabic Name" };
    private static readonly string[] CategoryAliases = { MenuTemplateWorkbook.CategoryHeader, "Category" };
    private static readonly string[] SubCategoryAliases = { MenuTemplateWorkbook.SubCategoryHeader, "Sub Category", "Sub-category" };
    private static readonly string[] PriceAliases = { MenuTemplateWorkbook.PriceHeader };
    private static readonly string[] DescriptionAliases = { MenuTemplateWorkbook.DescriptionHeader };
    private static readonly string[] AvailableAliases = { MenuTemplateWorkbook.AvailableHeader };

    private readonly IMenuItemRepository _menuItemRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ISubCategoryRepository _subCategoryRepository;
    private readonly ICategoryService _categoryService;
    private readonly ILogger<BulkMenuItemImportService> _logger;

    public BulkMenuItemImportService(
        IMenuItemRepository menuItemRepository,
        ICategoryRepository categoryRepository,
        ISubCategoryRepository subCategoryRepository,
        ICategoryService categoryService,
        ILogger<BulkMenuItemImportService> logger)
    {
        _menuItemRepository = menuItemRepository;
        _categoryRepository = categoryRepository;
        _subCategoryRepository = subCategoryRepository;
        _categoryService = categoryService;
        _logger = logger;
    }

    // Reads the two lists the dropdowns are built from and hands them to the workbook
    // builder. The building itself lives in MenuTemplateWorkbook so it can be exercised
    // with hand-written data and no database behind it.
    public async Task<Stream> GenerateTemplate()
    {
        var categories = await _categoryRepository.GetAllOrderedAsync();
        var subCategories = await _subCategoryRepository.GetAllOrderedAsync();

        var byCategoryId = subCategories
            .GroupBy(s => s.CategoryId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.OrderBy(s => s.DisplayOrder).Select(s => s.Name).ToList());

        var model = categories
            .Select(c => new TemplateCategory(
                c.Name,
                byCategoryId.TryGetValue(c.Id, out var subs) ? subs : Array.Empty<string>()))
            .ToList();

        return MenuTemplateWorkbook.Build(model);
    }

    public async Task<BulkMenuItemImportResult> ImportMenuItemsAsync(Stream fileStream)
    {
        var result = new BulkMenuItemImportResult();

        using var workbook = new XLWorkbook(fileStream);
        var sheet = workbook.Worksheets.First();

        // Columns are located by HEADER TEXT, not position. They used to be read by
        // fixed index, which meant changing the template's columns would silently
        // reinterpret any sheet downloaded before the change - an admin's Description
        // landing in Name, their Price in Category, on a live menu. Matching the header
        // makes the order irrelevant and keeps old sheets importing correctly.
        var columns = MapColumns(sheet.Row(1));
        if (!columns.TryGetValue(nameof(NameEnglishAliases), out var nameColumn))
        {
            result.Errors.Add(
                $"Could not find a \"{MenuTemplateWorkbook.NameEnglishHeader}\" column in row 1. " +
                "Download a fresh template if the header row was edited or removed.");
            return result;
        }

        int? Col(string key) => columns.TryGetValue(key, out var c) ? c : null;
        var arabicColumn = Col(nameof(NameArabicAliases));
        var categoryColumn = Col(nameof(CategoryAliases));
        var subCategoryColumn = Col(nameof(SubCategoryAliases));
        var priceColumn = Col(nameof(PriceAliases));
        var descriptionColumn = Col(nameof(DescriptionAliases));
        var availableColumn = Col(nameof(AvailableAliases));

        // Row 1 is the header row - data starts at row 2.
        var dataRows = sheet.RowsUsed().Skip(1);

        foreach (var row in dataRows)
        {
            var rowNumber = row.RowNumber();
            string Read(int? column) => column is null ? string.Empty : row.Cell(column.Value).GetString().Trim();

            var name = row.Cell(nameColumn).GetString().Trim();
            var nameAr = Read(arabicColumn);
            var description = Read(descriptionColumn);
            var priceRaw = Read(priceColumn);
            var categoryName = Read(categoryColumn);
            var subCategoryName = Read(subCategoryColumn);
            var availableRaw = Read(availableColumn);

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
                    // Blank cells leave what is already stored alone rather than
                    // wiping it - a sheet filled in for a price change should not clear
                    // every Arabic name the admin typed into the UI.
                    existing.Description = string.IsNullOrWhiteSpace(description) ? existing.Description : description;
                    existing.NameAr = string.IsNullOrWhiteSpace(nameAr) ? existing.NameAr : nameAr;
                    existing.Price = price;
                    existing.Category = category;
                    existing.IsAvailable = isAvailable;
                    existing.SubCategoryId = await ResolveSubCategoryIdAsync(subCategoryName, category) ?? existing.SubCategoryId;
                    _menuItemRepository.Update(existing);
                    result.ItemsUpdated++;
                }
                else
                {
                    await _menuItemRepository.AddAsync(new MenuItem
                    {
                        Name = name,
                        NameAr = string.IsNullOrWhiteSpace(nameAr) ? null : nameAr,
                        Description = string.IsNullOrWhiteSpace(description) ? null : description,
                        Price = price,
                        Category = category,
                        IsAvailable = isAvailable,
                        SubCategoryId = await ResolveSubCategoryIdAsync(subCategoryName, category)
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

    // Header text -> column index, keyed by the alias-array name so the caller asks for
    // a concept rather than a spelling. Matching is case- and whitespace-insensitive
    // because an admin who retypes a header rarely reproduces it exactly.
    private static Dictionary<string, int> MapColumns(IXLRow headerRow)
    {
        var aliasSets = new (string Key, string[] Aliases)[]
        {
            (nameof(NameEnglishAliases), NameEnglishAliases),
            (nameof(NameArabicAliases), NameArabicAliases),
            (nameof(CategoryAliases), CategoryAliases),
            (nameof(SubCategoryAliases), SubCategoryAliases),
            (nameof(PriceAliases), PriceAliases),
            (nameof(DescriptionAliases), DescriptionAliases),
            (nameof(AvailableAliases), AvailableAliases)
        };

        var found = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var cell in headerRow.CellsUsed())
        {
            var text = cell.GetString().Trim();
            if (text.Length == 0)
            {
                continue;
            }

            foreach (var (key, aliases) in aliasSets)
            {
                // First match wins: a sheet with both "Item Name" and "Name (English)"
                // should not have the later column quietly replace the earlier one.
                if (!found.ContainsKey(key) && aliases.Any(a => string.Equals(a, text, StringComparison.OrdinalIgnoreCase)))
                {
                    found[key] = cell.Address.ColumnNumber;
                    break;
                }
            }
        }

        return found;
    }

    // Matches a sub-category by name WITHIN the resolved category, creating one if it
    // does not exist yet - the same forgiving behaviour ResolveCategoryAsync already
    // has. Returns null for a blank cell, which leaves the item ungrouped rather than
    // inventing a sub-category nobody asked for.
    private async Task<int?> ResolveSubCategoryIdAsync(string subCategoryName, string categoryName)
    {
        if (string.IsNullOrWhiteSpace(subCategoryName))
        {
            return null;
        }

        var category = await _categoryRepository.GetByNameAsync(categoryName);
        if (category is null)
        {
            // The category is created moments earlier by ResolveCategoryAsync, so this
            // only happens if that failed - in which case the sub-category has nothing
            // to hang off and is better skipped than guessed at.
            return null;
        }

        var name = subCategoryName.Trim();
        var existing = await _subCategoryRepository.GetByNameInCategoryAsync(category.Id, name);
        if (existing is not null)
        {
            return existing.Id;
        }

        var siblings = await _subCategoryRepository.GetByCategoryIdOrderedAsync(category.Id);
        var created = new SubCategory
        {
            Name = name,
            CategoryId = category.Id,
            DisplayOrder = siblings.Count == 0 ? 0 : siblings[^1].DisplayOrder + 1
        };
        await _subCategoryRepository.AddAsync(created);
        await _subCategoryRepository.SaveChangesAsync();
        return created.Id;
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
