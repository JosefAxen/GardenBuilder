using GardenBuilder.SharedKernel;

namespace GardenBuilder.Modules.Catalog.Domain;

/// <summary>An item in the company's assortment. Only articles from the catalog can be used in a design.</summary>
internal sealed class Article
{
    private Article()
    {
        ArticleNumber = string.Empty;
        Name = string.Empty;
    }

    public Article(string articleNumber, string name, ArticleUnit unit, Money price, StockStatus stockStatus)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(articleNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Id = Ids.New();
        ArticleNumber = articleNumber;
        Name = name;
        Unit = unit;
        Price = price;
        StockStatus = stockStatus;
    }

    public Guid Id { get; private set; }

    public string ArticleNumber { get; private set; }

    public string Name { get; private set; }

    public ArticleUnit Unit { get; private set; }

    /// <summary>Price per unit in SEK including VAT.</summary>
    public Money Price { get; private set; }

    public StockStatus StockStatus { get; private set; }
}

internal enum ArticleUnit
{
    Piece,
    Meter,
    SquareMeter,
}

internal enum StockStatus
{
    InStock,
    OutOfStock,
    Discontinued,
}
