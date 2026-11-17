# Delprojekt 4: Trädgård och design

Status: beslutad. Josef sa "kör", så valen nedan är mina och står som sådana.

## Varför

Hittills försvinner allt kunden gör i editorn när fliken stängs. Det här delprojektet sparar kundens trädgård och
design på servern, så att kunden kan lämna och komma tillbaka. Det är grunden för allt som kommer sedan:
fototolkningen (8) fyller en design, designförslagen (9) skapar versioner, och offertförfrågan (11) låser en version.

## Mål

- Kundens design sparas automatiskt en sekund efter senaste ändring, utan någon spara-knapp.
- Kunden som kommer tillbaka i samma webbläsare hamnar direkt i sin senaste design.
- Två flikar kan aldrig tyst skriva över varandra (optimistisk låsning).
- En kund kommer bara åt sina egna designer. Regeln har egna tester.
- Designversioner som aldrig ändras kan skapas av andra moduler senare.
- Kunden ser alltid om designen är sparad.

## Icke-mål

- Lista över flera trädgårdar. Steget "Starta" i delprojekt 12 skapar trädgårdar med foton och adress.
- Foton, tomt och nuläge i Trädgård (delprojekt 8).
- Länk via e-post för att komma tillbaka från en annan dator (väntar på e-posttjänsten).
- Personalens åtkomst till designer (delprojekt 11) och delningslänkar (delprojekt 12).
- Knappar för versioner i webben. Ingen del av kundflödet skapar versioner än.
- Radering och export (delprojekt 13).

## Anti-mål

- En ändring som kunden gjort får aldrig försvinna utan att kunden ser det.
- En besökare som bara tittar ska inte skapa några kunder eller trädgårdar i databasen.

## Det kunden märker

- Editorn öppnar kundens senaste design. Finns ingen visas demoträdgården, och ingenting sparas förrän kunden ändrar
  något. Den första ändringen skapar kund, trädgård och design.
- I statusraden står "Sparad", "Sparar…" eller "Inte sparad, försöker igen…".
- Om designen har ändrats i en annan flik visas: "Designen har ändrats i en annan flik." med knapparna "Hämta den
  senaste" och "Behåll min".
- Försöker kunden stänga fliken med osparade ändringar varnar webbläsaren.

## Tillvägagångssätt

Övervägda sätt att skydda mot att två flikar skriver över varandra:

1. **Revisionsnummer med `If-Match` (valt).** Varje sparning skickar numret den utgick från. Servern sparar bara om
   numret stämmer och svarar annars 412. Standard i HTTP, en fråga i databasen, och editorn vet exakt när det krockar.
2. **Sist sparad vinner.** Enklast, men en gammal flik skriver tyst över en nyare design. Bryter mot anti-målet.
3. **Lås per flik.** En flik i taget får ändra. Kräver att låset släpps när fliken stängs, vilket webbläsare inte
   lovar. För tungt för en kund med två flikar.

Övervägda sätt att låta kunden komma tillbaka (Josef sa "kör" i stället för att välja):

1. **Senaste designen direkt (valt).** Lite att bygga, och räcker tills steget "Starta" finns.
2. **Sidan "Mina trädgårdar".** Passar regeln om flera trädgårdar, men kan behöva göras om i delprojekt 12.

## Domänen

**Trädgård (modul Garden, schema `garden`)**

- `Garden`: id, kund-id, skapad. Inget mer än så i det här delprojektet.
- Kontrakt `IGardens.CreateAsync(customerId)` skapar en trädgård och ger dess id.

**Design (modul Design, schema `design`)**

- `Design`: id, trädgårds-id, kund-id (ägaren), arbetskopia (scen som jsonb), revision (heltal från 1), skapad,
  ändrad.
  - Ägaren sparas på designen, så att varje sparning kan kontrollera åtkomsten utan att fråga Trädgård.
- `DesignVersion`: id, design-id, nummer (1, 2, 3 … per design), scen (jsonb), orsak, skapad. Ändras aldrig.
  - Orsaker: `ai-proposal`, `quote-request`, `staff-revision` (design.md, "Regler för data").
- Varje scen som sparas läses av `SceneJson` och kontrolleras av `SceneValidator`, samma som prisberäkningen.
- Kontrakt `IDesignVersions`:
  - `CreateAsync(designId, reason)` fryser arbetskopian som en ny version och ger id och nummer.
  - `GetAsync(versionId)` ger versionens scen.

## API:et

Alla svar för en design har formen `{ id, revision, scene }`. Ägarkontrollen görs i varje hanterare: en design som
tillhör någon annan svarar 404, precis som en som inte finns, så att ingen kan pröva vilka id:n som finns.

| Anrop | Gör | Svar |
| --- | --- | --- |
| `GET /api/designs/latest` | Kundens senast ändrade design. Skapar aldrig en kund. | 200, eller 404 utan kaka eller design |
| `GET /api/designs/{id}` | En design, till exempel efter en krock | 200 eller 404 |
| `POST /api/designs` | Kroppen är scenen. Skapar kund vid behov, trädgård och design med revision 1. | 201 med `ETag` |
| `PUT /api/designs/{id}/working-copy` | Kroppen är scenen, `If-Match` är revisionen den utgick från | 200 med ny `ETag`, 412, 428 eller 404 |

- Scenen läses som i prisberäkningen: högst 1 MB (413), oläslig ger 400 `unreadable-scene`, regelbrott ger 400
  `invalid-scene` med problemen.
- 412 har kroppen `{ code: "conflict", revision }` med den revision som gäller på servern.
- 428 när `If-Match` saknas.
- Sparningen är en enda fråga: uppdatera där id, ägare och revision stämmer, och öka revisionen. Påverkas ingen rad
  avgör en andra läsning om svaret är 404 eller 412.
- `POST` kontrollerar scenen innan något skapas, så att en ogiltig scen inte lämnar en tom trädgård efter sig.

## Editorn

- **Hämtning:** tills servern har svarat visas "Hämtar din design…" och ingenting sparas, annars kunde en ändring skapa
  en andra design bredvid kundens riktiga. Svarar servern inte står det "Det gick inte att hämta din design. Försöker
  igen…", och hämtningen görs om efter 2, 4, 8 … sekunder, högst 60.
- **Laddning:** `sceneStore` får ett kommando som inte är ett ångra-steg: `load(scene)`. Det ersätter dokumentet och
  tömmer historiken, markeringen och dragningen. Ångra kan alltså aldrig ta tillbaka själva laddningen.
- **Sparning:** en egen `saveStore`, skild från scenen på samma sätt som `priceStore`.
  - En sekund efter senaste ändring sparas dokumentet: `POST` första gången, därefter `PUT` med `If-Match`.
  - Bara en sparning åt gången. Ändringar under en pågående sparning sparas direkt efter den, med den nya revisionen.
  - Två snabba första ändringar ger en design, inte två.
  - Nätverksfel eller serverfel: "Inte sparad, försöker igen…", nytt försök efter 2, 4, 8 … sekunder, högst 60.
  - 412: krockrutan. "Hämta den senaste" laddar serverns version och kastar de egna ändringarna. "Behåll min" sparar
    med serverns revision.
  - 404 vid sparning: designen finns inte för den här kunden (till exempel ny kaka). Nästa sparning skapar en ny.
  - 400: "Designen kunde inte sparas." Inga nya försök, eftersom samma scen ger samma svar. Nästa ändring försöker igen.
- **Stängning:** vid `pagehide` med osparade ändringar skickas sparningen med `keepalive` om scenen är under 60 kB,
  webbläsarens gräns. `beforeunload` varnar så länge något är osparat.

## Begränsningar

- Moduler refererar inte varandra. Design anropar Trädgård genom `IGardens` i Contracts.
- Tabeller och kolumner i snake_case, id:n är UUID v7, tider i UTC.
- Migrationer körs av Migrator.
- Allt kunden ser är på svenska.

## Kända följder

- Den som rensar kakorna eller byter dator får en ny, tom kund. Den gamla trädgården ligger kvar utan väg tillbaka
  tills e-postlänken finns. Radering av sådana kommer med lagringstiderna i delprojekt 13.
- Varje sparning skickar hela scenen. En scen är några kilobyte, så det är billigare än att skicka ändringar.

## Lämnat till byggaren

- Exakta klass- och filnamn inom mönstret per funktion.
- Hur statusen ser ut i statusraden, inom editorns stil.
- Om `GetAsync` för versioner behövs i ett test redan nu eller först i delprojekt 11.
