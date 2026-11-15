namespace GardenBuilder.Contracts.Catalog;

/// <summary>One problem in an import file. <see cref="Row"/> is the Excel row; <see cref="Column"/> the header, or empty.</summary>
public sealed record CatalogImportError(int Row, string Column, string Message);

/// <summary>What an import did. When there are errors, nothing was written and all counts are 0.</summary>
public sealed record CatalogImportResult(int Created, int Updated, int Discontinued, IReadOnlyList<CatalogImportError> Errors)
{
    public bool Succeeded => Errors.Count == 0;
}

/// <summary>
/// Reads the company's articles from the Excel import file into the catalog. Used by the import tool now and by the
/// admin upload once staff can sign in.
/// </summary>
public interface ICatalogImport
{
    /// <summary>All or nothing: either every row is valid and the catalog is updated in one transaction, or nothing changes.</summary>
    Task<CatalogImportResult> ImportAsync(Stream xlsx, CancellationToken cancellationToken);

    /// <summary>Writes an import file with made-up example articles.</summary>
    void WriteExample(Stream xlsx);
}
