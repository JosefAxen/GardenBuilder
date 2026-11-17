# Modellbibliotek – design

Delprojekt 6 i [färdplanen](../plans/2026-11-13-roadmap.md). Källan för omfattning och regler är
[docs/design.md](../../design.md), avsnittet "Modellbibliotek".

## Syfte

Editorn ska visa en ritning av kundens egen trädgård. Kunden ska kunna peka på skärmen och säga "där är vår
thujahäck, där är äppelträdet". I dag ritas alla växter med några enkla former i kod
([models.ts](../../../src/web/src/editor/models.ts)), så en Thuja Smaragd och en Bergtall är samma kon i olika storlek.
Delprojektet byter de enkla formerna mot riktiga 3D-modeller ur Quaternius gratispaket, så att växterna går att känna
igen på formen och ser snygga ut.

Josef vill senare kunna visa en snygg demo när appen är klar. Modellerna ska vara bra nog för det, men själva demon
ingår inte här.

## Mål

- Omkring 15 igenkännbara former för det som står på en punkt (träd, buskar, perenner, sten, kruka, möbel). Varje
  artikel kopplas till den form som liknar den mest.
- Löv och barr har paketets egna färger, så det ser naturligt ut. Bara när artikelns bladfärg tydligt avviker från
  grönt (rödbladig japansk lönn, gulbladig Spirea Goldflame) tonas löven med artikelns färg.
- Blommor tonas alltid med artikelns accentfärg, så att en vit och en rosa hortensia syns olika.
- Källa och licens sparas för varje modell. Varje form har nominell storlek, markfäste och ett tak för antal trianglar.
- Filerna får innehållsbaserade namn och lång cachetid.

## Inte mål

- En modell per sort (alternativ C i samtalet). Det kräver modeller paketet inte har.
- Det betalda trädgårdspaketet. Beslut 2026-11-17: bara gratispaketet tills appen är klar.
- Molnlagring (Blob Storage) för modellfilerna, se Vägval.
- Enklare modeller på avstånd (LOD). Byggs bara om fps-mätningen visar under 60 fps.
- Häckar, staket, ytor och hus. De ritas fortsatt i kod ur scendokumentet.
- Demoträdgården och finare ljus för demon.
- Tillväxt i tiden (delprojekt 15).

## Det som inte får hända

- **3D-formen får aldrig bestämma vad något är.** Varje element i scenen pekar på sin artikel, och artikeln är
  sanningen: "Thuja Smaragd 120–140 cm", med höjd, bredd och färger. Formen ("smal pelare") är bara hur editorn ritar
  den. Scenen sparar aldrig formen, bara artikeln. När AI:n senare gör drömbilden (delprojekt 10) ska den få veta vilken
  artikel som står var, inte gissa från 3D-bilden.
- Priset får inte ändras av att en artikel byter form.

## Vägval

| | Vad | För | Emot | Valt |
| --- | --- | --- | --- | --- |
| **A** | Modellfilerna följer med webbappen. Formerna är en fast lista i koden. | Litet arbete. Vite ger innehållsbaserade namn och lång cachetid av sig själv. Ingen Azure behövs. | En ny form kräver en ny version av appen. | **Ja** |
| B | Modellfilerna i molnlagring, med en tabell över formerna i katalogens databas. | Nya modeller utan ny version. | Ungefär dubbelt arbete: uppladdning, tabell, API, adresser. Väntar på Azure. | Nej |

Josef valde A. Det avviker från färdplanens rad 6, som säger Blob Storage. En ny form behöver ändå kod för höjd och
markfäste, så en ny version kostar lite. Flyttar vi senare till molnlagring berör det bara var filerna hämtas ifrån.

För utseendet valde Josef "15–20 igenkännbara former" (B i samtalet) framför "6 snyggare former" och "en modell per
sort", och blandningen av färger ovan framför "paketets färger på allt" och "tona allt".

## Formerna

Valda efter att Josef sett dem renderade. Kolumnen "Familj" är den gamla typmodellen, som priset använder (se nedan).

| Nyckel | Form | Exempel på artiklar | Modell ur paketet | Familj |
| --- | --- | --- | --- | --- |
| `tree-large` | Stort lövträd | Björk, lönn, ek | `CommonTree_3` | `deciduous-tree` |
| `tree-fruit` | Fruktträd, brett och lågt | Äpple, körsbär, päron | `CommonTree_1`, gjord lägre och bredare | `deciduous-tree` |
| `tree-small` | Litet träd | Japansk lönn, magnolia | `CommonTree_5` | `deciduous-tree` |
| `pine` | Tall med hög stam | Tall | `Pine_1` | `conifer` |
| `spruce` | Gran, tät och spetsig | Serbisk gran, ädelgran | `Pine_5` | `conifer` |
| `conifer-column` | Pelarbarr, smal och hög | Thuja Smaragd, Brabant | `Bush_Common`, smal och hög, med trädens lövbild | `conifer` |
| `shrub-round` | Rund buske | Buxbom klot, Bergtall Mops | `Bush_Common` med trädens lövbild | `shrub` |
| `shrub-flowering` | Blommande buske | Hortensia, rhododendron, spirea, syren | `Bush_Common_Flowers` | `shrub` |
| `perennial-tall` | Hög perenn | Lavendel, rudbeckia, pion | `Flower_4_Group` | `perennial` |
| `perennial-low` | Låg perenn | Marktäckare | `Flower_3_Group` | `perennial` |
| `foliage-plant` | Bladväxt | Funkia, ormbunke | `Plant_1_Big` | `perennial` |
| `ornamental-grass` | Prydnadsgräs | Fjädergräs, elefantgräs | `Grass_Wispy_Tall` | `perennial` |
| `rock` | Sten | Dekorsten | `Rock_Medium_1` | `furniture` |
| `pot` | Kruka | Terrakottakruka | görs i kod som i dag | `pot` |
| `furniture` | Bänk | Trädgårdsbänk | görs i kod som i dag | `furniture` |

Linjer och ytor (`hedge`, `fence`, `surface-*`) är oförändrade.

*Mina val där Josef inte valt:* i de roller där jag visade flera alternativ valde jag `CommonTree_3` (stort lövträd),
`CommonTree_1` (fruktträd) och `Pine_5` (gran), som var mest igenkännbara i renderingen. `TwistedTree` är för tunga
(cirka 10 000 trianglar) och för sagolika. `DeadTree`, svampar, kronblad och småsten används inte.

`Bush_Common` har en blek lövbild i paketet, eftersom den är tänkt att färgas i en spelmotor. Den får i stället trädens
gröna lövbild (`Leaves_NormalTree_C`), som också är paketets egen.

## Familj och typmodell

I dag är typmodellen (till exempel `conifer`) både formen och det priset räknar med. Efter delprojektet är de två saker:

- **Typmodellen** är formen, till exempel `conifer-column`. Den står i katalogens Excelkolumn "Typmodell" och avgör hur
  artikeln ritas.
- **Familjen** är den gamla nyckeln, till exempel `conifer`. Den följer av typmodellen och används där sorten spelar roll
  för reglerna: borttagningsklass och arbetstid i prissättningen, vilka artiklar som är växter och ritas i
  planteringshöjd, och vilken sorts element artikeln blir.

Prissättningen läser familjen i stället för typmodellen, så priserna blir desamma som i dag.

Befintliga artiklar i databasen flyttas till nya nycklar av en migrering: `deciduous-tree` → `tree-large`,
`conifer` → `spruce`, `shrub` → `shrub-round` och `perennial` → `perennial-tall`. `pot` och `furniture` behåller sina
nycklar. Exempelkatalogen ([ExampleCatalog.cs](../../../src/Modules/Catalog/Features/ImportArticles/ExampleCatalog.cs))
får rätt form per artikel. Thujorna blir `conifer-column`, äpple och körsbär `tree-fruit`, Bergtall Mops `shrub-round`,
syren, hortensia, rhododendron och spirea `shrub-flowering`, och funkia `foliage-plant`. Excelimporten accepterar bara
de nya nycklarna. Ett felmeddelande för en gammal nyckel säger vilka nya nycklar som finns.

Migreringen kan bara flytta efter den gamla nyckeln, så efter den ritas till exempel thujor som gran och hortensior utan
blommor. Det rättas genom att exempelkatalogen importeras på nytt. I dag finns bara exempeldata, så det räcker.
När företagets riktiga lista kommer anger den formerna direkt.

## Färger

- **Löv och barr** ritas med paketets bild. När artikelns färg (Excelkolumnen "Färg") tydligt avviker från grönt, görs
  lövbilden grå och tonas med artikelns färg. Var gränsen för "tydligt avviker" går är byggarens val, men exempelkatalogen
  ska ge det här:
  - Japansk lönn (`#b5452f`) och Spirea Goldflame (`#9fbf45`) tonas.
  - De vanliga gröna (äpple `#5f9e3f`, björk `#8dbb4c`, thujorna `#3f7a3a` och `#4c8a3e`) tonas inte.
- **Blommor** görs grå och tonas alltid med artikelns accentfärg ("Accentfärg"). Utan accentfärg visas formen utan
  blommor, som i dag.
- **Stam, bark och sten** har alltid paketets färger.

När en bild görs grå och tonas, skalas ljusheten så att resultatet ungefär får tonfärgens egen färg och inte blir mörkt.
Det visade renderingen: en vanlig multiplikation med paketets färgade bild gör vit omöjlig och gör grönt mörkt.

## Filerna och verktyget

- Källfilerna för de valda modellerna ur paketet (`.gltf`, `.bin` och de texturer de använder) läggs i repot under
  `assets/models/quaternius/`, tillsammans med paketets licenstext (CC0 1.0) och adressen de kom ifrån
  (https://quaternius.com/packs/stylizednaturemegakit.html). Hela zip-filen läggs inte i repot.
- En förteckning (manifest) i repot säger för varje form: nyckel, familj, källmodell, eventuell lövbild att byta till,
  skalning på bredd och höjd, triangeltak, källa och licens.
- Ett verktyg gör om källfilerna till en GLB-fil per form. Det sätter markfästet mitt under modellen på marken, roterar
  rätt, byter lövbild där förteckningen säger det, slår ihop och komprimerar texturer och skriver ut formens nominella
  höjd och bredd. Utfilerna läggs i webbappen, där Vite ger dem innehållsbaserade namn.
- Ett test läser varje GLB och kontrollerar att den håller sitt triangeltak. Taket är 7 000 för träden (`tree-*`,
  `pine`, `spruce`) och 2 000 för alla andra former. *Mitt val efter granskningen:* `Flower_4_Group` har 1 690
  trianglar, så ett lägre tak för perenner hade tvingat fram en annan modell än den Josef såg. Ett annat test
  kontrollerar att Catalog-modulens lista och webbappens lista har samma nycklar.

## Editorn

- Varje form ritas med en `InstancedMesh` per del (material), som de genererade formerna i dag. Det gör 300 växter
  billigt.
- Växter skalas så att de får artikelns planteringshöjd, som i dag. Bredden följer formens proportioner.
- Tills GLB-filerna har laddats visas de enkla formerna i kod. Kryssrutan "Detaljerade modeller" byter mellan GLB och de
  enkla formerna, så att fps-mätningen i färdplanen fortfarande går att göra.
- Ringen för fullvuxen bredd och varningen för tät plantering är oförändrade.

## Det som är kvar åt byggaren

- Vilka verktyg som används för att göra om filerna, till exempel glTF Transform.
- Var gränsen går för "tydligt avviker från grönt", inom exemplen ovan.
- Exakt hur de enkla formerna i kod mappas mot de nya nycklarna medan GLB-filerna laddas.
- Hur kruka och bänk ritas. De behåller koden de har i dag, men kan putsas.

## Risker

- **Prestanda:** träden har 3 000–6 300 trianglar. En trädgård med 300 element blir tyngre än i dag. Mätningen på en
  vanlig bärbar dator avgör om LOD behövs.
- **Igenkänning:** några sorter blir "ungefär rätt", till exempel syren som blommande buske. Artikeln och dess namn
  är ändå rätt, så offert och drömbild påverkas inte.
