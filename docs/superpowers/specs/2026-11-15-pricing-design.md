# Prissättning – design

15 november 2026 · Delprojekt 5 i [färdplanen](../plans/2026-11-13-roadmap.md) · Bygger på [design.md](../../design.md)
och [katalogen](2026-11-14-catalog-design.md)

## Varför

Kunden kan i dag bygga en trädgård av artiklar ur katalogen men ser inget pris. Designdokumentet kräver att priset
uppdateras löpande som ett spann, att arbetskostnaden ingår och att servern alltid räknar. Prissättningen behövs också
senare för designförslag (delprojekt 9, budget) och offertförfrågan (delprojekt 11, fryst prisbild).

## Mål

- Editorn visar ett ungefärligt pris som ett spann, till exempel "42 000 – 51 000 kr", en halv sekund efter senaste
  ändringen.
- Priset består av material, arbete, borttagning och fasta poster, enligt design.md ("Beräkning").
- Spannet beror på varifrån måtten kommer: uppskattat ± 30 %, kundens mått ± 15 %, verifierat ± 5 %. Det läggs bara på
  rader som beror på yta eller längd. Styckvaror har exakt antal.
- Arbetstiden anges per artikel i katalogens Excelfil, i en ny kolumn "Arbete (h per enhet)" (valt av Josef).
- "Ta bort" på något som finns i trädgården i dag tar inte bort det ur scenen. Det markeras som "tas bort", visas
  grått och kostar borttagning.
- Servern räknar. Klienten visar bara resultatet.

## Icke-mål (kommer senare)

- Att spara uppskattningen med de priser som gällde. Det kräver sparade designer (delprojekt 4) och görs där.
- Att frysa prisbilden i en offertförfrågan. Det är delprojekt 11.
- Att ändra timpris, fasta poster och procentsatser i admin. Värdena ligger i databasen redan nu, men admin byggs i
  delprojekt 11. Fram till dess ändras de med en migrering.
- RUT- och ROT-avdrag. Frågan är öppen i design.md och påverkar bara hur priset visas, inte beräkningen.
- Olika timpris för olika sorters arbete.

## Anti-mål

- Ett pris som ser exakt ut fast måtten är uppskattade.
- Ett pris som räknas i webbläsaren. Då kan visat och offererat pris skilja sig åt.
- Priser som flyttal.
- Avrundning på varje rad. Avrundning sker en gång, på summan.
- Ett pris som tyst blir för lågt för att en artikel saknas. Saknade artiklar ska synas.

## Vägval

| Alternativ | För | Emot | Valt |
| --- | --- | --- | --- |
| A. Arbetstid per artikel, ny kolumn i Excelfilen | En plats att underhålla, olika storlekar kan ha olika arbetstid | En kolumn till i filen | **Ja**, valt av Josef |
| B. Arbetstid per typmodell i Pricing | Färre värden att fylla i | En 2 m thuja och en 50 cm thuja får samma arbetstid | Nej |
| C. Arbetstid per kategori | Mellanting | Kategorin är fritext och kan stavas olika | Nej |

| Alternativ | För | Emot | Valt |
| --- | --- | --- | --- |
| A. Editorn skickar hela scenen, servern räknar och svarar direkt | Enkelt, fungerar innan designer sparas | Scenen skickas varje gång (några kB) | **Ja** |
| B. Servern räknar på en sparad design | Ingen dubbel sändning | Kräver delprojekt 4 först | Nej |

## Hur

### Flödet

1. Kunden ändrar något i editorn. Editorn väntar en halv sekund efter senaste ändringen.
2. Editorn skickar scenen till `POST /api/pricing/estimate`. En tidigare fråga som inte hunnit svara avbryts.
3. Servern kontrollerar scenen med samma regler som Design-modulen och hämtar pris och arbetstid för artiklarna från
   Catalog.
4. En ren beräkningsfunktion i Pricing räknar ut raderna och summan.
5. Editorn visar resultatet i en prisruta.

### Katalogen får arbetstid

- Ny kolumn i Excelfilen: `Arbete (h per enhet)`. Obligatorisk, ett tal från 0 till 100 med högst två decimaler.
  Enheten följer artikelns enhet: timmar per styck, per meter eller per kvadratmeter.
- Nytt fält på `Article` och `ArticleData`. Befintliga artiklar i databasen får 0 genom migreringen, tills filen läses
  in igen.
- Exempelfilen får påhittade men rimliga värden, till exempel 0,3 h för en 50 cm thuja, 1,5 h för ett träd på 2 m,
  0,15 h per m² gräsmatta och 0,6 h per m² plattor.

### Kontrakt mellan modulerna

Pricing får inte referera till Catalog eller Design. Därför två nya gränssnitt i `src/Contracts`:

- `ICatalogPrices` (Catalog implementerar): tar en lista med artikel-id och svarar med id, namn, enhet, pris,
  arbetstid, typmodell och om artikeln har utgått, för varje artikel som finns. Enhet och typmodell skickas som
  textnycklar (`piece`, `deciduous-tree`), som i `GET /api/catalog/articles`, eftersom Catalogs egna typer är interna.
- `ISceneCheck` (Design implementerar): kontrollerar en scen med `SceneValidator` och svarar med felkoderna. Pricing
  räknar inte på en ogiltig scen. Kontrollen behövs också för att `Polygon` och `Polyline` kastar fel vid för få
  punkter.

*Mitt beslut efter granskningen:* Pricing litar inte på att webbläsaren skickar rätt. Pekar ett element på en artikel
med fel enhet, till exempel en punkt på en artikel som säljs per m², räknas elementet som saknat.

### Beräkningen

Beräkningen är en ren funktion i `Pricing/Domain`: scen, artikelfakta och prisinställningar in, uppskattning ut. Den
läser ingen databas och är därför lätt att testa.

**Mängd per element**

| Element | Mängd |
| --- | --- |
| Yta | Area i m² (`Polygon.Area`) |
| Linje | Längd i m (`Polyline.Length`) |
| Punkt | 1 st |
| Tomt, byggnad | Ingen rad |

**Rader per status**

| Status | Rader |
| --- | --- |
| Ny | Material: pris × mängd. Arbete: arbetstid × mängd × timpris. |
| Befintlig, tas bort | Borttagning: borttagningstid × mängd × timpris. |
| Befintlig, behålls | Inga rader. |

Element som pekar på samma artikel slås ihop till en rad per artikel och sort (material, arbete). Borttagning blir en
rad per borttagningsklass. En sammanslagen rads låga och höga belopp är summan av elementens låga och höga belopp.
Rader på 0 kr visas inte, till exempel arbete för en artikel som ännu har arbetstid 0.

**Borttagningstid**

Befintliga element har ofta ingen artikel, till exempel en gräsmatta från fototolkningen. Borttagningstiden hämtas
därför från en tabell per borttagningsklass:

| Klass | Bestäms av | Tid (påhittat startvärde) |
| --- | --- | --- |
| Lövträd | typmodell `deciduous-tree`, och punkter utan artikel | 3 h per st |
| Barrträd | typmodell `conifer` | 2 h per st |
| Buske eller annan punkt | övriga punkter med artikel | 0,5 h per st |
| Häck | typmodell `hedge`, och linjer utan artikel | 0,8 h per m |
| Staket | typmodell `fence` | 0,4 h per m |
| Gräsmatta | ytor med material `grass` | 0,2 h per m² |
| Plattor | ytor med material `paving` | 0,6 h per m² |
| Grus | ytor med material `gravel` | 0,3 h per m² |
| Trädäck | ytor med material `decking` | 0,5 h per m² |
| Rabatt | ytor med material `bed` | 0,3 h per m² |

*Mitt beslut efter granskningen:* en punkt utan artikel räknas som lövträd, den dyraste klassen, så att priset inte
tyst blir för lågt. Ytor delas efter materialet, som scenen redan har. Saknas artikeln till ett element som tas bort,
väljs klassen på samma sätt som för ett element utan artikel. Fototolkningen (delprojekt 8) kan senare ge elementen en
artikel.

**Fasta poster**

| Post | När | Belopp (påhittat startvärde) |
| --- | --- | --- |
| Etablering | När det finns minst en rad för material, arbete eller borttagning | 2 500 kr |
| Transport | Samma som etablering | 1 500 kr |
| Bortforsling | När det finns minst en rad för borttagning | 2 000 kr |

En scen med bara tomt och hus, eller där allt behålls, kostar 0 kr och visar inga fasta poster. En scen utan tomt är
ogiltig och ger fel, inte 0 kr.

**Spann**

- Varje rad har ett lågt och ett högt belopp.
- Rader för ytor och linjer får spann efter elementets måttkälla: ± 30 %, ± 15 % eller ± 5 %. Det gäller material,
  arbete och borttagning, eftersom alla beror på mängden.
- Punkter och fasta poster har inget spann.
- Är tomten markerad som sluttande räknas ytor och linjer en nivå osäkrare: verifierat som kundens mått, kundens mått
  som uppskattat. Uppskattat förblir uppskattat.
- Säkerhetsnivån för hela priset är den lägsta nivån bland de ytor och linjer som ger en rad, alltså nya och
  borttagna, efter att sluttningen räknats in. Behållna element och tomten räknas inte. Finns inga sådana ytor eller
  linjer är nivån hög.

**Moms och avrundning**

- Katalogens priser, timpriset och de fasta posterna är inklusive moms, som allt kunden ser. Momsen visas därför som
  "varav moms".
- Momssatsen sparas som 25 %. Andelen moms i ett pris inklusive moms räknas fram ur den: 25 / 125 = 20 %.
- Raderna och delsummorna räknas med decimaler. Avrundning sker en gång, på totalen: det låga beloppet avrundas nedåt
  och det höga uppåt till närmaste hundratal kronor. "42 300 – 51 100 kr" ser inte exaktare ut än det är.
- "Varav moms" räknas ur den avrundade totalen och avrundas till hela kronor. Den är bara information och påverkar
  inget annat belopp.

*Mitt beslut efter granskningen:* hela kronor som "42 317 – 51 042 kr" ser exakt ut, vilket design.md vill undvika.
Därför hundratal.

**Saknade artiklar**

Ett nytt element räknas som saknat om dess artikel inte finns, har utgått eller har fel enhet. Det ger ingen rad.
Svaret säger hur många element det gäller, och prisrutan visar "2 saker i designen saknas i katalogen och ingår inte i
priset." Utgångna artiklar räknas som saknade, eftersom de inte går att beställa och editorn redan visar dem som
saknade.

### Prisinställningar

- Ligger i Pricing-modulens schema i databasen: timpris (650 kr inklusive moms), procentsatser per måttkälla, fasta
  poster, momssats och borttagningstider per klass.
- Startvärdena är påhittade och läggs in med migreringen. Företaget ställer in riktiga värden i admin senare.
- Beräkningen får inställningarna som ett vanligt objekt, så att testerna kan använda egna värden.

*Granskaren föreslog att vänta med tabellen tills admin finns. Josef valde databasen, så att platsen för värdena är
bestämd från början.*

### API

`POST /api/pricing/estimate` läser scendokumentet med `SceneJson`, samma strikta läsning som resten av systemet. Ett
dokument som inte går att läsa ger 400. Svaret har strängar i kebab-case, som scenen:

- `lines`: en rad per artikel och sort eller per borttagningsklass eller fast post, med text på svenska, sort
  (`material`, `labor`, `removal`, `fixed`), mängd, enhet, lågt och högt belopp.
- `subtotals`: lågt och högt för material, arbete, borttagning och fasta poster, så att webbläsaren inte räknar.
- `total`: lågt och högt, avrundat.
- `vat`: lågt och högt, varav moms.
- `confidence`: `low`, `medium` eller `high`.
- `missingArticles`: antal nya element vars artikel saknas.

En ogiltig scen ger 400 med felkoderna från `SceneValidator`. Prisrutan visar då "Priset kan inte räknas, eftersom
designen har fel." Beräkningen är snabb och sparar ingenting, så den behöver ingen kö.

### Editorn

**Prisrutan**

- Visar totalspannet stort, till exempel "42 300 – 51 100 kr", och säkerhetsnivån: "Säkerhet: låg", "medel" eller
  "hög", med förklaringen "Spannet blir snävare när måtten är uppmätta."
- Under det: material, arbete, borttagning och fasta poster som summor, och "varav moms".
- "Visa alla rader" fäller ut raderna från servern.
- Alltid raden "Priset är ungefärligt. Den bindande offerten kommer från säljaren."
- Medan ett nytt pris hämtas visas det gamla blekt med "Räknar…". Fel visas på svenska, och hämtningen har en
  tidsgräns som katalogen.

**Ta bort och byt på befintligt**

- "Ta bort" på ett nytt element tar bort det, som i dag.
- "Ta bort" på ett befintligt element sätter status "tas bort". Det ritas grått och halvgenomskinligt och kostar
  borttagning.
- Ett element som tas bort kan väljas, och då visas bara "Behåll", som sätter tillbaka status "behålls". Det går inte
  att flytta eller byta det.
- "Byt" på ett befintligt element markerar det gamla som "tas bort" och lägger till ett nytt element med nytt id,
  samma plats, rotation, form och måttkälla, och status "ny". Båda stegen är ett kommando, så ett "Ångra" räcker.
- Kommandona får tester, som de andra kommandona.

### Design.md uppdateras i samma ändring

- "Moms läggs på sist och visas för sig" blir "priserna är inklusive moms, och momsen visas som varav moms".
- Arbetsnormen ligger på artikeln i katalogen, inte i Prissättning.
- Att varje uppskattning sparar de priser som gällde flyttas till delprojekt 4.

## Lämnat till byggaren

- Exakta namn på klasser, metoder, tabeller och kolumner.
- Hur prisinställningarna lagras i databasen, så länge de går att ändra utan ny kod senare.
- Hur ett borttaget element ritas i detalj, så länge det syns tydligt att det tas bort och inte flimrar mot ett nytt
  element på samma plats.
- Exakta arbetstider i exempelfilen, så länge de är rimliga för en trädgårdsanläggare.
