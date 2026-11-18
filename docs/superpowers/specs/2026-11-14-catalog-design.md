# Katalog – design

14 november 2026 · Delprojekt 2 i [färdplanen](../plans/2026-11-13-roadmap.md) · Bygger på [design.md](../../design.md)

## Varför

Editorn bygger i dag trädgårdar av en påhittad lista i koden (`src/web/src/editor/demoCatalog.ts`). Designdokumentet
kräver att varje design består enbart av artiklar ur företagets lager, och att AI:n bara föreslår sådant som finns och
klarar kundens växtzon. Katalogen är därför grunden för editorn nu och för prissättning (delprojekt 5) och
designförslag (delprojekt 9) senare.

## Mål

- En artikel har artikelnummer, namn, kategori, enhet, pris inklusive moms, lagerstatus och typmodell, enligt
  design.md ("Moduler och domänmodell"). Växter har dessutom växtzon, fullvuxen höjd och bredd samt ljusbehov.
- Artiklar läses in från en Excelfil med ett importverktyg som körs på datorn.
- Editorns katalogpanel hämtar artiklarna från `GET /api/catalog/articles` i stället för listan i koden.
- En växt syns i designen i den storlek kunden köper den, inte fullvuxen. En 50 cm thuja ser ut som 50 cm.
- Varje storlek är en egen artikel med eget artikelnummer och pris, som i en plantskolas prislista (valt av Josef).
  Kunden byter storlek med "Byt", och platsen behålls.
- En exempelfil med påhittade artiklar finns att importera. Thujor ska ingå i flera storlekar: Tuja Smaragd och
  Tuja Brabant som styckväxter och thujahäck per meter.

## Icke-mål (kommer senare)

- Uppladdning av Excelfilen i admin i webben. Den kräver inloggning och byggs efter delprojekt 3, med samma
  importkod som verktyget.
- Riktiga 3D-modeller (GLB) och en tabell för typmodeller. Det är delprojekt 6. Fram till dess är typmodellerna en fast
  lista i koden, och modellerna genereras i kod som i prototypen.
- Prislistor, arbetsnormer och prisregler. Det är delprojekt 5.
- Lagersaldon. "Lager" betyder sortiment med status, inte antal (antagande i design.md).
- Kategorier som egen tabell. Kategorin är en text på artikeln.
- Att visa hur trädgården ser ut fullvuxen, och ett skjutreglage som låter växterna växa i realtid från planteringshöjd
  till fullvuxen storlek. Josef vill ha det senare; det ligger i färdplanen. Fullvuxen storlek och tillväxttakt sparas
  redan nu, så att Excelmallen inte behöver ändras när det byggs.

## Anti-mål

- En import som läser in halva filen. Antingen läses hela filen in, eller ingenting.
- En artikel som försvinner trots att en sparad design pekar på den.
- Priser som flyttal.
- En import som går att köra av vem som helst över nätet.

## Vägval

| Alternativ | För | Emot | Valt |
| --- | --- | --- | --- |
| A. Importverktyg på datorn (`tools/CatalogImport`) | Säkert direkt, litet, samma importkod kan senare kopplas till admin | Ingen knapp i webben förrän inloggning finns | **Ja**, valt av Josef |
| B. Uppladdningsknapp i webben nu, med tillfälligt lås | Närmare slutflödet | Tillfälligt lås är lätt att glömma; säkerhet stryks aldrig enligt design.md | Nej |
| C. Inloggning först, katalog sedan | Rätt ordning för säkerheten | Editorn väntar längre; kräver e-posttjänst som inte är vald | Nej |

## Hur

### Artikeln

Fält på `Article` i Catalog-modulen (befintliga i fetstil):

| Fält | Typ | Regel |
| --- | --- | --- |
| **Id** | UUID v7 | Sätts vid första import och ändras aldrig |
| **Artikelnummer** | text, max 64 | Unikt. Nyckeln som importen känner igen en artikel på |
| **Namn** | text, max 200 | Krävs. Innehåller storleken, t.ex. "Tuja Smaragd 80–100 cm" |
| Sort | text, max 200 | Valfri. Artiklar med samma sort, t.ex. "Tuja Smaragd", är samma växt i olika storlekar och visas samlade i editorn |
| Kategori | text, max 100 | Krävs, t.ex. "Träd", "Buskar", "Häckar och staket" |
| **Enhet** | st / m / m² | Måste passa typmodellen (se nedan) |
| **Pris** | `Money` i SEK inklusive moms | Större än 0, högst två decimaler |
| **Lagerstatus** | I lager / Slut / Utgått | |
| Typmodell | en nyckel ur listan nedan | Krävs |
| Skala | tal | Större än 0, högst 5. Standard 1. Används bara för det som inte är växter (krukor, möbler) |
| Färg och accentfärg | `#rrggbb` | Färg krävs, accent valfri |
| Växtzon | heltal 1–8 | Krävs för typmodeller som är växter, annars tom. Den kallaste zonen växten klarar, som i plantskolornas märkning: 5 betyder zon I–V. Designförslagen använder bara växter med växtzon ≥ kundens zon |
| Höjd | meter, större än 0 | Växter och häckar: planteringshöjden, alltså den höjd plantan har när den levereras. Staket: staketets höjd. Krävs för dem, tomt för övriga |
| Bredd | meter, större än 0 | Häckar och staket: linjens bredd. Krävs för dem, tomt för övriga |
| Fullvuxen höjd | meter, större än 0 | Valfri. Uppgift om växten, ritas inte |
| Fullvuxen bredd | meter, större än 0 | Krävs för punktväxter (träd, buskar, perenner), eftersom avståndsringen och varningen för tät plantering bygger på den. Valfri för häckar |
| Klipphöjd | meter, större än 0 | Valfri, bara häckar: den höjd häcken hålls på. Uppgift, ritas inte |
| Tillväxt | centimeter per år, större än 0 | Valfri, bara växter. Används senare av skjutreglaget för tillväxt |
| Ljus | en eller flera av sol / halvskugga / skugga | Krävs för växter. I Excel kommaseparerat, t.ex. "sol, halvskugga" |

*Mina beslut efter granskningen och Josefs ändring:*
- Ljus får vara flera värden, eftersom växtlistor ofta säger "sol–halvskugga" och AI-urvalet senare ska matcha
  kundens ljusläge.
- Växter ritas i planteringshöjd, eftersom det är det kunden köper. Bredden följer modellens proportioner. Fullvuxen
  storlek ritas inte. En thuja som Smaragd kan bli nästan hur hög som helst oklippt, så "fullvuxen" säger lite om vad
  som står i trädgården.
- Ringen på marken visar fullvuxen bredd, så kunden ser hur mycket plats växten tar med tiden.
- Skala används bara för det som inte är växter (krukor, möbler).

### Typmodeller (fast lista tills delprojekt 6)

*Ändrat i delprojekt 6:* punkternas typmodeller är nu former, och de gamla nycklarna nedan är deras familjer. Se
[designen för modellbiblioteket](2026-11-17-model-library-design.md) för listan över former.

| Nyckel | Geometri i scenen | Enhet | Växt |
| --- | --- | --- | --- |
| `deciduous-tree` | punkt | st | ja |
| `conifer` | punkt | st | ja |
| `shrub` | punkt | st | ja |
| `perennial` | punkt | st | ja |
| `pot` | punkt | st | nej |
| `furniture` | punkt | st | nej |
| `hedge` | linje | m | ja |
| `fence` | linje | m | nej |
| `surface-grass` | yta, material `grass` | m² | nej |
| `surface-paving` | yta, material `paving` | m² | nej |
| `surface-gravel` | yta, material `gravel` | m² | nej |
| `surface-decking` | yta, material `decking` | m² | nej |
| `surface-bed` | yta, material `bed` | m² | nej |

Typmodellen bestämmer både hur artikeln ritas och vilken sorts element den kan bli i scenen. Därför måste enheten
passa: en häck kan inte säljas per styck.

*Mitt beslut efter granskningen:* ytorna är fem typmodeller, en per material i scenformatet (`SurfaceMaterial`). När en
ytartikel läggs i scenen sätts elementets `material` från typmodellen, så scenen och katalogen säger samma sak.

### Importen

- Excelfilen har ett blad med en rubrikrad och en artikel per rad. Rubrikerna är på svenska:
  `Artikelnummer, Namn, Sort, Kategori, Enhet, Pris inkl moms (kr), Lagerstatus, Typmodell, Skala, Färg, Accentfärg,
  Växtzon, Höjd (m), Bredd (m), Fullvuxen höjd (m), Fullvuxen bredd (m), Klipphöjd (m), Tillväxt (cm/år), Ljus`. Kolumnordningen spelar
  ingen roll, rubrikerna gör det.
- Alla rader kontrolleras först. Finns ett enda fel läses ingenting in, och verktyget skriver ut alla fel med
  radnummer och kolumn på svenska.
- Artiklar matchas på artikelnummer. Finns numret uppdateras artikeln, annars skapas den.
- En artikel som finns i databasen men saknas i filen tas inte bort. Den får lagerstatus Utgått, eftersom sparade
  designer kan peka på den.
- Importen körs i en databastransaktion.
- Verktyget har två kommandon: `import <fil.xlsx>` och `example <fil.xlsx>`, som skriver exempelfilen. Anslutningen
  till databasen tas från `ConnectionStrings__gardenbuilder`, som för Migrator.
- Lokalt finns verktyget som en resurs i Aspire (`catalog-import`) som bara startar när man trycker Start i
  dashboarden. Den väntar på Migrator, får anslutningen av Aspire och importerar exempelfilen
  `assets/catalog/artiklar-exempel.xlsx`, som ligger i kodförrådet. Med `dotnet run` och en egen anslutningssträng
  kan andra filer importeras.

*Mitt beslut efter granskningen:* Aspire ger databasen ett nytt lösenord och kan byta port, så ett verktyg som körs
för sig hittar inte databasen utan hjälp. En knapp i dashboarden är enklast och kräver inget lösenord i någon fil.
- Excelfiler läses och skrivs med ClosedXML (MIT-licens).

### API

`GET /api/catalog/articles` returnerar alla artiklar utom Utgått, med alla fält ovan. Utgångna artiklar behövs senare
för att visa gamla designer; det löses när sparade designer finns (delprojekt 4).

### Editorn

- Katalogpanelen hämtar artiklarna från API:et och visar "Laddar katalogen…" och ett felmeddelande på svenska om
  hämtningen misslyckas.
- Artiklar med samma sort visas samlade under sortnamnet, med en rad per storlek. När en planta är vald visas
  "Byt" först för de andra storlekarna av samma sort.
- Artiklar med status Slut visas men går inte att lägga till.
- Startscenen innehåller bara tomten, huset och en befintlig gräsmatta utan artikel, eftersom artiklarnas id nu sätts av
  servern vid import. Stresstestet med 300 växter använder artiklar från API:et.
- `demoCatalog.ts` tas bort.

## Lämnat till byggaren

- Exakta namn på klasser, metoder och kolumner i databasen.
- Hur importfelen formuleras, så länge de är på svenska och anger rad och kolumn.
- Exakt vilka påhittade artiklar exempelfilen innehåller, utöver thujorna. Ungefär 30 artiklar spridda över alla
  typmodeller. Thujorna ska finnas i minst tre storlekar av Smaragd, två av Brabant och två av thujahäck.
