using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using GardenBuilder.Modules.Catalog.Domain;
using GardenBuilder.SharedKernel;

namespace GardenBuilder.Modules.Catalog.Features.ImportArticles;

/// <summary>One problem in the import file. <see cref="Row"/> is the Excel row number; <see cref="Column"/> is the header, or empty.</summary>
internal sealed record ImportError(int Row, string Column, string Message);

/// <summary>The articles in a file, or the errors that stop it. When there is any error, <see cref="Articles"/> is empty.</summary>
internal sealed record SheetResult(IReadOnlyList<ArticleData> Articles, IReadOnlyList<ImportError> Errors);

/// <summary>
/// Reads and writes the catalog Excel file (the import template). People type these files by hand, so reading is
/// forgiving about form (decimal comma, spaces, numbers stored as text, header case) and strict about content.
/// </summary>
internal static partial class ArticleSheet
{
    private const string ArticleNumber = "Artikelnummer";
    private const string Name = "Namn";
    private const string Variety = "Sort";
    private const string Category = "Kategori";
    private const string Unit = "Enhet";
    private const string Price = "Pris inkl moms (kr)";
    private const string Labor = "Arbete (h per enhet)";
    private const string Stock = "Lagerstatus";
    private const string Model = "Typmodell";
    private const string Scale = "Skala";
    private const string Color = "Färg";
    private const string Accent = "Accentfärg";
    private const string Zone = "Växtzon";
    private const string Height = "Höjd (m)";
    private const string Width = "Bredd (m)";
    private const string MatureHeight = "Fullvuxen höjd (m)";
    private const string MatureWidth = "Fullvuxen bredd (m)";
    private const string ClipHeight = "Klipphöjd (m)";
    private const string Growth = "Tillväxt (cm/år)";
    private const string LightColumn = "Ljus";

    /// <summary>The columns in the order the template writes them.</summary>
    public static readonly IReadOnlyList<string> Columns =
    [
        ArticleNumber, Name, Variety, Category, Unit, Price, Labor, Stock, Model, Scale, Color, Accent, Zone, Height, Width,
        MatureHeight, MatureWidth, ClipHeight, Growth, LightColumn,
    ];

    private static readonly int PriceColumn = IndexOf(Price);
    private static readonly int LaborColumn = IndexOf(Labor);

    /// <summary>The Excel column each ArticleData field comes from, so a broken rule can point at its column.</summary>
    private static readonly Dictionary<string, string> ColumnOf = new(StringComparer.Ordinal)
    {
        [nameof(ArticleData.ArticleNumber)] = ArticleNumber,
        [nameof(ArticleData.Name)] = Name,
        [nameof(ArticleData.Variety)] = Variety,
        [nameof(ArticleData.Category)] = Category,
        [nameof(ArticleData.Unit)] = Unit,
        [nameof(ArticleData.Price)] = Price,
        [nameof(ArticleData.LaborHours)] = Labor,
        [nameof(ArticleData.Scale)] = Scale,
        [nameof(ArticleData.Color)] = Color,
        [nameof(ArticleData.AccentColor)] = Accent,
        [nameof(ArticleData.HardinessZone)] = Zone,
        [nameof(ArticleData.Height)] = Height,
        [nameof(ArticleData.Width)] = Width,
        [nameof(ArticleData.MatureHeight)] = MatureHeight,
        [nameof(ArticleData.MatureWidth)] = MatureWidth,
        [nameof(ArticleData.ClipHeight)] = ClipHeight,
        [nameof(ArticleData.GrowthCmPerYear)] = Growth,
        [nameof(ArticleData.Light)] = LightColumn,
    };

    public static SheetResult Read(Stream xlsx)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(xlsx);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // ClosedXML throws different exceptions for files that are not .xlsx (a .csv, an old .xls, a damaged file).
            return new SheetResult([], [new ImportError(1, string.Empty, "Filen går inte att läsa som en Excelfil (.xlsx).")]);
        }

        using var disposeWorkbook = workbook;
        var sheet = workbook.Worksheets.First();

        var positions = sheet.Row(1).CellsUsed()
            .GroupBy(c => Normalize(c.GetString()))
            .ToDictionary(g => g.Key, g => g.First().Address.ColumnNumber, StringComparer.Ordinal);
        var missing = Columns.Where(c => !positions.ContainsKey(Normalize(c))).ToList();
        if (missing.Count > 0)
        {
            return new SheetResult([], [.. missing.Select(c => new ImportError(1, c, $"Kolumnen {c} saknas."))]);
        }

        var articles = new List<ArticleData>();
        var errors = new List<ImportError>();
        var firstRowOfNumber = new Dictionary<string, int>(StringComparer.Ordinal);
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;

        for (var row = 2; row <= lastRow; row++)
        {
            var reader = new RowReader(sheet.Row(row), positions);
            if (reader.IsBlank)
            {
                continue;
            }

            var data = reader.ReadArticle();
            if (reader.Errors.Count > 0)
            {
                errors.AddRange(reader.Errors);
                continue;
            }

            if (firstRowOfNumber.TryGetValue(data.ArticleNumber, out var firstRow))
            {
                errors.Add(new ImportError(row, ArticleNumber, $"Artikelnumret {data.ArticleNumber} finns redan på rad {firstRow}."));
                continue;
            }

            firstRowOfNumber[data.ArticleNumber] = row;
            var ruleErrors = data.RuleErrors();
            errors.AddRange(ruleErrors.Select(e => new ImportError(row, ColumnOf.GetValueOrDefault(e.Field, string.Empty), e.Message)));
            if (ruleErrors.Count == 0)
            {
                articles.Add(data);
            }
        }

        // An empty file would otherwise discontinue the whole catalog.
        if (errors.Count == 0 && articles.Count == 0)
        {
            errors.Add(new ImportError(2, string.Empty, "Filen innehåller inga artiklar."));
        }

        return errors.Count > 0 ? new SheetResult([], errors) : new SheetResult(articles, []);
    }

    public static void Write(IEnumerable<ArticleData> articles, Stream xlsx)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Artiklar");
        for (var c = 0; c < Columns.Count; c++)
        {
            sheet.Cell(1, c + 1).Value = Columns[c];
        }

        sheet.Row(1).Style.Font.Bold = true;
        var row = 2;
        foreach (var a in articles)
        {
            object?[] values =
            [
                a.ArticleNumber, a.Name, a.Variety, a.Category, ArticleData.UnitText(a.Unit), (double)a.Price.Amount,
                (double)a.LaborHours,
                StockText(a.StockStatus), TypeModels.Key(a.TypeModel), a.Scale, a.Color, a.AccentColor, a.HardinessZone,
                a.Height, a.Width, a.MatureHeight, a.MatureWidth, a.ClipHeight, a.GrowthCmPerYear, LightText(a.Light),
            ];
            for (var c = 0; c < values.Length; c++)
            {
                var cell = sheet.Cell(row, c + 1);
                switch (values[c])
                {
                    case string text when text.Length > 0:
                        cell.Value = text;
                        break;
                    case double number:
                        cell.Value = number;
                        break;
                    case int number:
                        cell.Value = number;
                        break;
                }
            }

            sheet.Cell(row, Columns.Count).Style.NumberFormat.Format = "@";
            sheet.Cell(row, PriceColumn).Style.NumberFormat.Format = "0.00";
            sheet.Cell(row, LaborColumn).Style.NumberFormat.Format = "0.00";
            row++;
        }

        sheet.Columns().AdjustToContents();
        workbook.SaveAs(xlsx);
    }

    /// <summary>The 1-based Excel column number of a header in the template.</summary>
    private static int IndexOf(string column) => Columns.ToList().IndexOf(column) + 1;

    /// <summary>Headers match regardless of case and of how the spaces were typed (doubled, non-breaking).</summary>
    private static string Normalize(string header) =>
        Whitespace().Replace(header.Normalize(NormalizationForm.FormC), " ").Trim().ToLowerInvariant();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    private static string StockText(StockStatus status) => status switch
    {
        StockStatus.InStock => "I lager",
        StockStatus.OutOfStock => "Slut",
        _ => "Utgått",
    };

    private static string LightText(Light light) => string.Join(", ", new[]
    {
        light.HasFlag(Light.Sun) ? "sol" : null,
        light.HasFlag(Light.PartShade) ? "halvskugga" : null,
        light.HasFlag(Light.Shade) ? "skugga" : null,
    }.OfType<string>());

    /// <summary>Reads one row cell by cell and collects every cell error, so the person sees all problems at once.</summary>
    private sealed class RowReader(IXLRow row, IReadOnlyDictionary<string, int> positions)
    {
        public List<ImportError> Errors { get; } = [];

        public bool IsBlank => Columns.All(c => Text(c) is null);

        public ArticleData ReadArticle() => new(
            ArticleNumber: Text(ArticleNumber) ?? string.Empty,
            Name: Text(Name) ?? string.Empty,
            Variety: Text(Variety),
            Category: Text(Category) ?? string.Empty,
            Unit: Required(Unit, ParseUnit, "Använd st, m eller m²."),
            Price: Money.InSek(Required(Price, ParseDecimal, "Skriv priset som ett tal, t.ex. 495 eller 1 295,50.")),
            StockStatus: Required(Stock, ParseStock, "Använd I lager, Slut eller Utgått."),
            TypeModel: Required(Model, ParseTypeModel, "Se listan över typmodeller i designen."),
            Scale: Optional(Scale, ParseDouble) ?? 1,
            Color: Text(Color) ?? string.Empty,
            AccentColor: Text(Accent),
            HardinessZone: Optional(Zone, ParseInt),
            Height: Optional(Height, ParseDouble),
            Width: Optional(Width, ParseDouble),
            MatureHeight: Optional(MatureHeight, ParseDouble),
            MatureWidth: Optional(MatureWidth, ParseDouble),
            ClipHeight: Optional(ClipHeight, ParseDouble),
            GrowthCmPerYear: Optional(Growth, ParseDouble),
            Light: ReadLight(),
            LaborHours: Required(Labor, ParseDecimal, "Skriv arbetstiden i timmar, t.ex. 0,5."));

        private int RowNumber => row.RowNumber();

        private IXLCell Cell(string column) => row.Cell(positions[Normalize(column)]);

        /// <summary>The cell as text; numbers written invariantly. Null when blank.</summary>
        private string? Text(string column)
        {
            var value = Cell(column).Value;
            // G15 is the precision Excel shows: a formula like 19.9*3 gives 59.699999999999996 as a double but shows 59,70.
            var text = value.IsNumber ? value.GetNumber().ToString("G15", CultureInfo.InvariantCulture) : value.ToString(CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        private T Required<T>(string column, Func<string, T?> parse, string hint)
            where T : struct
        {
            var text = Text(column);
            if (text is null)
            {
                Errors.Add(new ImportError(RowNumber, column, $"{column} saknas."));
                return default;
            }

            if (parse(text) is { } value)
            {
                return value;
            }

            Errors.Add(new ImportError(RowNumber, column, $"\"{text}\" går inte att läsa. {hint}"));
            return default;
        }

        private T? Optional<T>(string column, Func<string, T?> parse)
            where T : struct
        {
            var text = Text(column);
            if (text is null)
            {
                return null;
            }

            var value = parse(text);
            if (value is null)
            {
                Errors.Add(new ImportError(RowNumber, column, $"\"{text}\" är inte ett tal."));
            }

            return value;
        }

        private Light ReadLight()
        {
            var light = Light.None;
            foreach (var part in (Text(LightColumn) ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                switch (part.ToLowerInvariant())
                {
                    case "sol":
                        light |= Light.Sun;
                        break;
                    case "halvskugga":
                        light |= Light.PartShade;
                        break;
                    case "skugga":
                        light |= Light.Shade;
                        break;
                    default:
                        Errors.Add(new ImportError(RowNumber, LightColumn, $"Okänt ljus \"{part}\". Använd sol, halvskugga eller skugga."));
                        break;
                }
            }

            return light;
        }
    }

    /// <summary>Accepts "1295.5", "1295,5" and "1 295,50"; spaces (also non-breaking) are thousands separators.</summary>
    private static decimal? ParseDecimal(string text)
    {
        var cleaned = new string([.. text.Where(c => !char.IsWhiteSpace(c))]).Replace(',', '.');
        return decimal.TryParse(cleaned, NumberStyles.Number & ~NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static double? ParseDouble(string text) => ParseDecimal(text) is { } value ? (double)value : null;

    private static int? ParseInt(string text) =>
        ParseDecimal(text) is { } value && value == decimal.Truncate(value) && value is >= int.MinValue and <= int.MaxValue ? (int)value : null;

    private static ArticleUnit? ParseUnit(string text) => text.ToLowerInvariant() switch
    {
        "st" => ArticleUnit.Piece,
        "m" => ArticleUnit.Meter,
        "m²" or "m2" => ArticleUnit.SquareMeter,
        _ => null,
    };

    private static StockStatus? ParseStock(string text) => text.ToLowerInvariant() switch
    {
        "i lager" => StockStatus.InStock,
        "slut" => StockStatus.OutOfStock,
        "utgått" => StockStatus.Discontinued,
        _ => null,
    };

    private static TypeModel? ParseTypeModel(string text) => TypeModels.TryParse(text.ToLowerInvariant(), out var model) ? model : null;
}
