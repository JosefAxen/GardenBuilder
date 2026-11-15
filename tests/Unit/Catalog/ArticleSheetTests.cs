using ClosedXML.Excel;
using GardenBuilder.Modules.Catalog.Domain;
using GardenBuilder.Modules.Catalog.Features.ImportArticles;
using GardenBuilder.SharedKernel;

namespace GardenBuilder.UnitTests.Catalog;

public sealed class ArticleSheetTests
{
    private static readonly string[] Headers =
    [
        "Artikelnummer", "Namn", "Sort", "Kategori", "Enhet", "Pris inkl moms (kr)", "Lagerstatus", "Typmodell", "Skala",
        "Färg", "Accentfärg", "Växtzon", "Höjd (m)", "Bredd (m)", "Fullvuxen höjd (m)", "Fullvuxen bredd (m)",
        "Klipphöjd (m)", "Tillväxt (cm/år)", "Ljus",
    ];

    /// <summary>Tuja Smaragd 80–100 cm as a person would type it into Excel.</summary>
    private static object?[] ThujaRow(string articleNumber = "TU-SM-080") =>
    [
        articleNumber, "Tuja Smaragd 80–100 cm", "Tuja Smaragd", "Träd", "st", "495", "I lager", "conifer", "1",
        "#3f7a3a", null, "5", "0,9", null, "4", "1,2", null, "20", "sol, halvskugga",
    ];

    /// <summary>Builds an Excel file in memory. Strings become text cells, numbers become number cells, null stays blank.</summary>
    private static MemoryStream Sheet(string[] headers, params object?[][] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Artiklar");
        for (var c = 0; c < headers.Length; c++)
        {
            sheet.Cell(1, c + 1).Value = headers[c];
        }

        for (var r = 0; r < rows.Length; r++)
        {
            for (var c = 0; c < rows[r].Length; c++)
            {
                var cell = sheet.Cell(r + 2, c + 1);
                switch (rows[r][c])
                {
                    case string formula when formula.StartsWith('='):
                        cell.FormulaA1 = formula[1..];
                        break;
                    case string text:
                        cell.Value = text;
                        break;
                    case double number:
                        cell.Value = number;
                        break;
                }
            }
        }

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    private static object?[] With(object?[] row, string header, object? value)
    {
        var copy = (object?[])row.Clone();
        copy[Array.IndexOf(Headers, header)] = value;
        return copy;
    }

    [Fact]
    public void Written_example_reads_back_identically()
    {
        using var stream = new MemoryStream();
        ArticleSheet.Write(ExampleCatalog.Articles, stream);
        stream.Position = 0;

        var result = ArticleSheet.Read(stream);

        Assert.Empty(result.Errors);
        Assert.Equal(ExampleCatalog.Articles, result.Articles);
    }

    [Fact]
    public void A_typed_row_reads_as_the_article()
    {
        var result = ArticleSheet.Read(Sheet(Headers, ThujaRow()));

        Assert.Empty(result.Errors);
        var thuja = Assert.Single(result.Articles);
        Assert.Equal(TypeModel.Conifer, thuja.TypeModel);
        Assert.Equal(0.9, thuja.Height);
        Assert.Equal(1.2, thuja.MatureWidth);
        Assert.Null(thuja.AccentColor);
    }

    [Theory]
    [InlineData("1295,5")]
    [InlineData("1 295,50")]
    [InlineData("1295.50")]
    public void Price_reads_with_comma_point_and_spaces(string text)
    {
        var result = ArticleSheet.Read(Sheet(Headers, With(ThujaRow(), "Pris inkl moms (kr)", text)));

        Assert.Empty(result.Errors);
        Assert.Equal(Money.InSek(1295.50m), Assert.Single(result.Articles).Price);
    }

    [Fact]
    public void Price_stored_as_number_reads_the_same()
    {
        var result = ArticleSheet.Read(Sheet(Headers, With(ThujaRow(), "Pris inkl moms (kr)", 1295.5)));

        Assert.Equal(Money.InSek(1295.50m), Assert.Single(result.Articles).Price);
    }

    [Fact]
    public void Price_calculated_by_a_formula_reads_as_shown_in_excel()
    {
        // The formula 19.9*3 evaluates to 59.699999999999996; Excel shows 59,70, and so must the import.
        var result = ArticleSheet.Read(Sheet(Headers, With(ThujaRow(), "Pris inkl moms (kr)", "=19.9*3")));

        Assert.Empty(result.Errors);
        Assert.Equal(Money.InSek(59.70m), Assert.Single(result.Articles).Price);
    }

    [Fact]
    public void A_file_without_articles_is_an_error()
    {
        // Otherwise an import of the wrong (empty) file would discontinue the whole catalog.
        var result = ArticleSheet.Read(Sheet(Headers, new object?[Headers.Length]));

        Assert.Contains(result.Errors, e => e.Message == "Filen innehåller inga artiklar.");
    }

    [Fact]
    public void Headers_ignore_case_and_spaces()
    {
        var headers = (string[])Headers.Clone();
        headers[0] = "  artikelnummer ";

        var result = ArticleSheet.Read(Sheet(headers, ThujaRow()));

        Assert.Empty(result.Errors);
        Assert.Single(result.Articles);
    }

    [Fact]
    public void Missing_column_is_named()
    {
        var headers = Headers.Where(h => h != "Typmodell").ToArray();

        var result = ArticleSheet.Read(Sheet(headers));

        Assert.Contains(new ImportError(1, "Typmodell", "Kolumnen Typmodell saknas."), result.Errors);
    }

    [Fact]
    public void Blank_rows_are_skipped()
    {
        var result = ArticleSheet.Read(Sheet(Headers, ThujaRow("A-1"), new object?[Headers.Length], ThujaRow("A-2")));

        Assert.Empty(result.Errors);
        Assert.Equal(2, result.Articles.Count);
    }

    [Fact]
    public void Bad_value_names_row_and_column()
    {
        var result = ArticleSheet.Read(Sheet(Headers, ThujaRow("A-1"), ThujaRow("A-2"), With(ThujaRow("A-3"), "Pris inkl moms (kr)", "gratis")));

        var error = Assert.Single(result.Errors);
        Assert.Equal(4, error.Row);
        Assert.Equal("Pris inkl moms (kr)", error.Column);
        Assert.Empty(result.Articles);
    }

    [Fact]
    public void Rule_errors_carry_the_row()
    {
        var result = ArticleSheet.Read(Sheet(Headers, With(ThujaRow(), "Växtzon", null)));

        Assert.Contains(new ImportError(2, "", "Växtzon krävs för växter."), result.Errors);
    }

    [Fact]
    public void Duplicate_article_number_is_an_error_on_the_second_row()
    {
        var result = ArticleSheet.Read(Sheet(Headers, ThujaRow("A-1"), ThujaRow("A-1")));

        var error = Assert.Single(result.Errors);
        Assert.Equal(3, error.Row);
        Assert.Equal("Artikelnummer", error.Column);
    }

    [Fact]
    public void Light_reads_several_values()
    {
        var result = ArticleSheet.Read(Sheet(Headers, ThujaRow()));

        Assert.Equal(Light.Sun | Light.PartShade, Assert.Single(result.Articles).Light);
    }

    [Fact]
    public void Square_meter_accepts_m2()
    {
        var lawn = With(With(With(ThujaRow(), "Enhet", "m2"), "Typmodell", "surface-grass"), "Kategori", "Ytor");
        foreach (var plantColumn in new[] { "Sort", "Växtzon", "Höjd (m)", "Fullvuxen höjd (m)", "Fullvuxen bredd (m)", "Tillväxt (cm/år)", "Ljus" })
        {
            lawn = With(lawn, plantColumn, null);
        }

        var result = ArticleSheet.Read(Sheet(Headers, lawn));

        Assert.Empty(result.Errors);
        Assert.Equal(ArticleUnit.SquareMeter, Assert.Single(result.Articles).Unit);
    }
}
