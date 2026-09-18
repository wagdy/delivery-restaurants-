using ClosedXML.Excel;

namespace RestaurantDelivery.Infrastructure.Excel;

// One category and the sub-categories under it, as the template needs them. A plain
// record rather than the EF entities on purpose: this class builds a spreadsheet and
// nothing else, so it can be exercised with hand-written data and no database - which
// is the only way the dropdown wiring below gets checked without opening Excel.
public sealed record TemplateCategory(string Name, IReadOnlyList<string> SubCategories);

public static class MenuTemplateWorkbook
{
    // The column set the template ships with. ImportMenuItemsAsync matches these by
    // HEADER TEXT rather than position, so adding a column here cannot silently shift
    // what an already-filled-in sheet imports as.
    public const string NameArabicHeader = "Name (Arabic)";
    public const string NameEnglishHeader = "Name (English)";
    public const string CategoryHeader = "Main Category";
    public const string SubCategoryHeader = "Subcategory";
    public const string PriceHeader = "Price";
    public const string DescriptionHeader = "Description";
    public const string AvailableHeader = "Available";

    public static readonly string[] Headers =
    {
        NameArabicHeader,
        NameEnglishHeader,
        CategoryHeader,
        SubCategoryHeader,
        PriceHeader,
        DescriptionHeader,
        AvailableHeader
    };

    public const string DataSheetName = "Menu Items";
    public const string DropdownSheetName = "DropdownData";

    // Headroom past the example rows so the dropdowns still work for however many items
    // the admin pastes in beyond them.
    private const int DataRowCount = 500;

    public static Stream Build(IReadOnlyList<TemplateCategory> categories)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(DataSheetName);

        for (var i = 0; i < Headers.Length; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = Headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(63, 81, 181);
            cell.Style.Font.FontColor = XLColor.White;
        }

        var sample = categories.FirstOrDefault();
        var sampleCategory = sample?.Name ?? string.Empty;
        var sampleSub = sample?.SubCategories.FirstOrDefault() ?? string.Empty;

        var sampleRows = new object[,]
        {
            { "شاورما دجاج", "Chicken Shawarma Wrap", sampleCategory, sampleSub, 9.99, "Grilled chicken, garlic sauce, pickles.", "Yes" },
            // Second row deliberately leaves category, sub-category and the Arabic name
            // blank: all three are optional, and showing that is more useful than a
            // second fully-populated row that implies they are required.
            { "", "Baklava", "", "", 4.5, "Layered filo pastry with nuts and honey.", "Yes" }
        };

        for (var row = 0; row < sampleRows.GetLength(0); row++)
        {
            for (var col = 0; col < sampleRows.GetLength(1); col++)
            {
                sheet.Cell(row + 2, col + 1).Value = XLCellValue.FromObject(sampleRows[row, col]);
            }
        }

        // Arabic reads right-to-left; without this the column is legible but sits the
        // wrong way round against the text an admin is pasting in.
        sheet.Column(1).Style.Alignment.ReadingOrder = XLAlignmentReadingOrderValues.RightToLeft;

        var lastDataRow = sampleRows.GetLength(0) + 1 + DataRowCount;
        AddDropdowns(workbook, sheet, categories, lastDataRow);

        sheet.Columns().AdjustToContents();
        sheet.SheetView.FreezeRows(1);

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    private static void AddDropdowns(
        XLWorkbook workbook,
        IXLWorksheet sheet,
        IReadOnlyList<TemplateCategory> categories,
        int lastDataRow)
    {
        var data = workbook.Worksheets.Add(DropdownSheetName);

        // Column A holds every category; each following column holds one category's
        // sub-categories, headed by the category name. That layout is what lets the
        // sub-category dropdown be dependent: the header cell names the range.
        data.Cell(1, 1).Value = "Categories";
        data.Cell(1, 1).Style.Font.Bold = true;
        for (var i = 0; i < categories.Count; i++)
        {
            data.Cell(i + 2, 1).Value = categories[i].Name;
        }

        // Referencing at least one real row keeps the formula valid on a brand-new
        // restaurant with no categories yet - an empty range makes Excel reject it.
        var lastCategoryRow = Math.Max(categories.Count, 1) + 1;
        var categorySource = $"'{DropdownSheetName}'!$A$2:$A${lastCategoryRow}";

        ApplyList(
            sheet.Range($"C2:C{lastDataRow}"),
            categorySource,
            "Not an existing category",
            "This will be created as a new category on import. Leave blank to use \"Uncategorized\".");

        // --- dependent sub-category lists -------------------------------------------
        // Excel resolves a dependent list through INDIRECT on a defined name, and a
        // defined name cannot contain spaces or start with a digit. Any category whose
        // name will not survive that becomes part of the flat fallback list instead of
        // silently producing a dropdown that resolves to nothing.
        var column = 2;
        var dependentNames = new Dictionary<string, string>(StringComparer.Ordinal);
        var allSubCategories = new List<string>();

        foreach (var category in categories)
        {
            foreach (var sub in category.SubCategories)
            {
                if (!allSubCategories.Contains(sub, StringComparer.Ordinal))
                {
                    allSubCategories.Add(sub);
                }
            }

            if (category.SubCategories.Count == 0)
            {
                continue;
            }

            var definedName = ToDefinedName(category.Name);
            if (definedName is null || dependentNames.ContainsValue(definedName))
            {
                // Unusable or colliding after sanitising - skip the dependent range for
                // this one. Its sub-categories are still reachable from the flat list.
                continue;
            }

            data.Cell(1, column).Value = category.Name;
            data.Cell(1, column).Style.Font.Bold = true;
            for (var i = 0; i < category.SubCategories.Count; i++)
            {
                data.Cell(i + 2, column).Value = category.SubCategories[i];
            }

            var columnLetter = data.Column(column).ColumnLetter();
            var range = $"'{DropdownSheetName}'!${columnLetter}$2:${columnLetter}${category.SubCategories.Count + 1}";
            workbook.DefinedNames.Add(definedName, range);
            dependentNames[category.Name] = definedName;
            column++;
        }

        if (dependentNames.Count == categories.Count(c => c.SubCategories.Count > 0) && dependentNames.Count > 0)
        {
            // Every category with sub-categories got a usable defined name, so the
            // dependent formula can be trusted for all of them. SUBSTITUTE mirrors the
            // sanitising in ToDefinedName so the lookup matches the name that was added.
            var formula = $"=INDIRECT(SUBSTITUTE($C2,\" \",\"_\"))";
            ApplyList(
                sheet.Range($"D2:D{lastDataRow}"),
                formula,
                "Not a sub-category of that category",
                "Pick the Main Category first - this list follows it. A new name here is created on import.");
        }
        else
        {
            // Mixed or unusable names: a flat list of every sub-category is honest and
            // works, where a dependent formula would quietly show an empty dropdown for
            // the categories that could not be named.
            var flatColumn = column;
            data.Cell(1, flatColumn).Value = "All sub-categories";
            data.Cell(1, flatColumn).Style.Font.Bold = true;
            for (var i = 0; i < allSubCategories.Count; i++)
            {
                data.Cell(i + 2, flatColumn).Value = allSubCategories[i];
            }

            var letter = data.Column(flatColumn).ColumnLetter();
            var lastRow = Math.Max(allSubCategories.Count, 1) + 1;
            ApplyList(
                sheet.Range($"D2:D{lastDataRow}"),
                $"'{DropdownSheetName}'!${letter}$2:${letter}${lastRow}",
                "Not an existing sub-category",
                "This will be created under the Main Category on import.");
        }

        data.Columns().AdjustToContents();
        // Hidden, not deleted - still reachable through Excel's Sheet > Unhide for
        // anyone who wants to read or edit the lists.
        data.Visibility = XLWorksheetVisibility.Hidden;
    }

    private static void ApplyList(IXLRange range, string source, string errorTitle, string errorMessage)
    {
        var validation = range.CreateDataValidation();
        validation.List(source, inCellDropdown: true);
        validation.IgnoreBlanks = true;
        // Information, not Stop: a name that is not in the list yet is a legitimately
        // new category or sub-category, which the importer creates. Stop would make
        // typing one impossible.
        validation.ErrorStyle = XLErrorStyle.Information;
        validation.ErrorTitle = errorTitle;
        validation.ErrorMessage = errorMessage;
    }

    // Excel defined names allow letters (including Arabic), digits, underscore, period
    // and backslash, must not start with a digit, and must not look like a cell
    // reference. Returns null when a name cannot be made to fit, so the caller can fall
    // back rather than emit a dropdown that silently resolves to nothing.
    internal static string? ToDefinedName(string categoryName)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            return null;
        }

        var chars = categoryName.Trim().Select(c => c == ' ' ? '_' : c).ToArray();
        if (chars.Any(c => !(char.IsLetterOrDigit(c) || c is '_' or '.')))
        {
            return null;
        }

        var name = new string(chars);
        if (char.IsDigit(name[0]) || name.Length > 255)
        {
            return null;
        }

        // "C1", "R2" and friends are cell references, not usable as defined names.
        if (name.Length <= 4 && name.Skip(1).All(char.IsDigit) && char.IsLetter(name[0]))
        {
            return null;
        }

        return name;
    }
}
