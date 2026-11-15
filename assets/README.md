# 3D assets

Tools for optimizing 3D models (GLB) before they are uploaded to Blob Storage. Model files themselves are not
committed here.

Rules (docs/design.md, "Modellbibliotek"):
- Only licenses that allow commercial use in a web app. Record source and license for every model in `models.csv`.
- Each type model has a nominal size, ground anchor and a triangle budget.
- File names are content hashes, served with a long cache time.

## Catalog

`catalog/artiklar-exempel.xlsx` holds made-up example articles in the import template's format. It is generated from
`ExampleCatalog` in the Catalog module; regenerate it after changing that list:

```bash
dotnet run --project tools/CatalogImport -- example assets/catalog/artiklar-exempel.xlsx
```

A unit test fails when the committed file and `ExampleCatalog` differ.
