# Drömträdgård – designdokument

Nov 13, 2026 · @Josef

## Syfte

Drömträdgård är ett säljverktyg för ett företag som planerar, säljer och anlägger trädgårdar. Kunden laddar upp foton på sin trädgård, får den uppbyggd som en stiliserad 3D-scen och får designförslag från AI. Sedan formar kunden sin egen design ur företagets lager och får ett ungefärligt pris.

Flödet slutar med en offertförfrågan. En säljare öppnar samma design i admin, justerar den och skickar den bindande offerten.

Dokumentet beskriver vad som ska byggas och varför, så att en utvecklare kan planera arbetet utan att fråga om grunderna. Den detaljerade genomförandeplanen skrivs när designen är godkänd.

## Mål, icke-mål och anti-mål

Version 1 ska ta en kund från foto till offertförfrågan utan hjälp, och bara med artiklar som finns i lagret.

**Mål**

- Kunden går från uppladdat foto till skickad offertförfrågan på egen hand.
- Varje design består enbart av artiklar ur företagets lager.
- Priset visas som ett spann som blir snävare när kunden anger egna mått.
- Säljaren öppnar och redigerar kundens design i admin.
- Kunden kan spara designen och dela den som länk eller PDF.
- Appen är driftklar för riktiga kunder senast den 13 december 2026.

**Icke-mål i version 1**

- Direktbeställning och betalning.
- Skannad, fotorealistisk 3D av kundens eget hus.
- Unik 3D-modell för varje artikel.
- Sluttningar och nivåskillnader. Marken är platt i scenen.
- Stöd för flera företag i samma installation.
- Koppling till ett befintligt affärs- eller lagersystem. Artiklar importeras från Excel.
- Redigering i 3D på mobil. Mobilen klarar uppladdning och visning.

**Anti-mål, det som gör produkten misslyckad även om den fungerar**

- Ett pris som ser exakt ut fast det bygger på uppskattade mått.
- AI som föreslår växter som saknas i lagret eller inte klarar kundens växtzon.
- En drömbild som visar något annat än den design kunden får offert på.
- En kund som fastnar för att fototolkningen misslyckades.
- AI-kostnader utan tak.

## Förutsättningar

En utvecklare bygger hela systemet på en månad, och det ska tåla riktiga kunder från första dagen.

- **Bemanning:** en utvecklare, med Claude som kodstöd.
- **Tid:** start måndag 16 november, klart senast söndag 13 december 2026.
- **Teknik:** backend i .NET, frontend i React och TypeScript.
- **Marknad:** ett svenskt företag, svenska som språk, priser i kronor inklusive moms.
- **Personuppgifter:** foton, adresser och kontaktuppgifter lagras inom EU och hanteras enligt GDPR.
- **Beroenden utanför koden:** företagets artikellista med priser, 3D-modeller från Quaternius och ett trädgårdspaket och riktiga trädgårdsfoton att testa mot.

## Användarflöden

Kunden går igenom sex steg, och varje steg har en manuell reservväg så att ingen fastnar när AI:n gör fel.

**Kundens flöde**

1. **Starta.** Kunden laddar upp ett till sex foton, anger adress och bekräftar sin e-post med en engångskod.
   - Tomtens form och skala hämtas från kartdata om det går. Annars ritar kunden tomt och hus och anger två mått.
2. **Nuläge.** AI:n tolkar fotona och bygger en stiliserad 3D-scen. Kunden rättar det som blev fel.
   - Reservväg: en tom tomt med huset utplacerat, där kunden själv lägger in det som finns.
3. **Förslag.** Kunden anger stil, budgetram, skötselnivå och växtzon. AI:n ger två till tre designförslag ur lagret.
   - Reservväg: kunden börjar från nuläget och designar själv.
4. **Egen design.** Kunden byter ut, flyttar och lägger till artiklar ur katalogen. Priset uppdateras löpande som ett spann.
5. **Drömbild.** Kunden väljer en kameravinkel och får en fotorealistisk bild av designen.
6. **Offertförfrågan.** Kunden kan lägga in egna mått för ett snävare pris och skickar sedan förfrågan.

Kunden kan när som helst spara, lämna och komma tillbaka via en länk i e-posten.

**Säljarens flöde i admin**

1. Säljaren ser nya förfrågningar i en ärendelista med design, artikellista, pris och säkerhetsnivå.
2. Säljaren öppnar designen i samma 3D-editor och justerar den som en ny version.
3. Efter platsbesök markerar säljaren måtten som verifierade, vilket snävar in priset.
4. Säljaren skapar offerten som en redigerbar radlista och skickar den som PDF.

Ett ärende går genom statusarna Ny, Under arbete, Platsbesök bokat, Offert skickad och till sist Vunnen eller Förlorad.

**Administratörens arbete**

- Sköter artiklar, priser, arbetsnormer och lagerstatus, med import från Excel.
- Kopplar varje artikel till en 3D-typmodell.
- Ställer in prisregler, AI-kvoter och lagringstider.
- Raderar eller exporterar en kunds uppgifter på begäran.

## Vägval

Fem beslut är dina och formar produkten. Övriga är mina förslag och kan ändras utan att produkten blir en annan.

| Beslut | Valt | Bortvalt | Skäl | Beslutat av |
| --- | --- | --- | --- | --- |
| Flödets slut | Offertförfrågan, säljaren redigerar, frivilliga mått | Direktbeställning. Enbart säljarverktyg. | Uppskattade mått tål inte ett bindande pris. | Du |
| Vad kunden redigerar | 3D-scen | Planritning med AI-bild. Enbart AI-bild. | Upplevelsen väger tyngst. Kräver modellbibliotek och mer editorarbete. | Du |
| Igenkänning | Stiliserad 3D med AI-drömbild | Enbart stiliserad 3D. Skannad trädgård. | Robust att bygga, och drömbilden ger realismen. | Du |
| Frontend | React och TypeScript | Blazor med inbäddad 3D-modul | Editorns paneler och scen delar tillstånd utan språkgräns. | Du |
| 3D-stil | Stiliserad: Quaternius, ett trädgårdspaket och kodgenererade former | Lågpolygonal. Skannad realism (Megascans). Icke-kommersiella licenser. | Lätt i webbläsaren, tydlig att redigera i och fri att använda kommersiellt. Drömbilden ger realismen. | Du |
| 3D-motor | three.js via React Three Fiber | Babylon.js | Scenen ritas ur samma tillstånd som resten av gränssnittet. | Jag |
| Backend | Modulär monolit med en arbetsprocess | Mikrotjänster | En utvecklare och en driftsättning, med tydliga gränser ändå. | Jag |
| Plattform | .NET 10 (LTS) | .NET 11 | .NET 11 släpptes 10 november 2026. Version 10 har ett års mognad och [stöd till november 2028](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core). | Jag |
| Databas | PostgreSQL | Azure SQL | Scenen lagras som JSON-dokument, och grundkostnaden är lägre. | Jag |
| Jobbkö | Jobbtabell i PostgreSQL | Azure Service Bus | Jobb och data sparas i samma transaktion, och det blir en tjänst mindre. | Jag |
| Förlopp till klienten | Klienten frågar varannan sekund | SignalR | Fungerar med flera instanser utan extra tjänst. | Jag |
| Drift | Azure, region Sweden Central | Egen server med Docker | Hanterad databas, backup och hemligheter sparar tid. | Jag |
| Tomtens form | Kunden ritar, kartdata som förbättring | Enbart kartdata | Lantmäteriets data är avgiftsfri men [kräver registrering och ansökan](https://www.lantmateriet.se/sv/geotorget-produktstod/om-tillhandahallandet/). | Jag |
| Prisberäkning | Enbart på servern | Även i klienten | En implementation ger inget glapp mellan visat och offererat pris. | Jag |
| AI-leverantör | Bakom egna gränssnitt, modell väljs i prototypen | Hårdkodad leverantör | Modeller byts ofta, och valet ska bygga på mätning. | Jag |

Valet av 3D-scen framför planritning är det som kostar mest tid. Det är därför editorn och modellbiblioteket provas redan första veckan.

.NET 11 är en STS-version med två års stöd enligt [Microsofts lansering](https://devblogs.microsoft.com/dotnet/announcing-dotnet-11/). En uppgradering från version 10 kan göras efter lanseringen.

## Systemarkitektur

Systemet är en webbklient, ett API och en arbetsprocess som delar databas och fillagring.

&#91;embedded content: systemarkitektur · klient, API, arbetsprocess, lagring och externa tjänster\]

Webbklienten talar bara med API:et. Arbetsprocessen är den enda delen som anropar AI-leverantörerna.

**Ett AI-jobb steg för steg**

1. Klienten ber API:et starta ett jobb, till exempel en fototolkning.
2. API:et kontrollerar behörighet och kvot och sparar jobbet i kön.
3. Arbetsprocessen hämtar jobbet, anropar AI-leverantören och validerar svaret.
4. Resultatet sparas, och jobbet markeras som klart eller misslyckat.
5. Klienten frågar efter status varannan sekund och hämtar resultatet när det är klart.

**Det här ses över när systemet växer**

- Jobbtabellen byts mot en kötjänst om volymen blir hög.
- Statusfrågorna byts mot att servern skickar händelser till klienten.
- Webbklienten och 3D-modellerna flyttas till ett innehållsnät.
- Arbetsprocessen skalas automatiskt efter köns längd.

## Moduler och domänmodell

Backend är en applikation med sju moduler. Varje modul äger sina data och nås bara genom sitt publika gränssnitt.

| Modul | Ansvar | Centrala entiteter |
| --- | --- | --- |
| Trädgård | Kundens tomt, foton och nuläge | Trädgård, Foto, Tomt, Nulägestolkning |
| Katalog | Företagets sortiment och 3D-modeller | Artikel, Kategori, Typmodell, Lagerstatus |
| Design | Scener och deras versioner | Design, Arbetskopia, Designversion |
| Prissättning | Prislistor, regler och beräkning | Prislista, Arbetsnorm, Prisregel, Prisuppskattning |
| Offertärenden | Förfrågningar, ärendestatus och offerter | Offertförfrågan, Ärendehändelse, Offert |
| AI | Jobb, promptar, leverantörer och kostnader | AI-jobb, Promptversion, Kostnadspost |
| Identitet | Kunder, personal, roller och samtycken | Användare, Roll, Samtycke |

Kedjan mellan dem är: en kund har trädgårdar, en trädgård har designer, en design har versioner, och en offertförfrågan låser en version med en prisbild.

En artikel har artikelnummer, namn, kategori, enhet, pris, lagerstatus och typmodell. Växter har dessutom växtzon, fullvuxen höjd och bredd samt ljusbehov. De fälten styr importmallen för Excel.

**Regler för modulgränser**

- Varje modul har ett eget schema i databasen. Ingen modul läser en annan moduls tabeller.
- Moduler anropar varandra genom ett kontraktsprojekt med gränssnitt och enkla dataobjekt.
- Arkitekturtester bryter bygget om en modul refererar en annan moduls inre delar.
- Inom en modul ordnas koden per funktion, med slutpunkt, hanterare och validering i samma mapp.
- Affärsregler ligger i domänobjekt där regler finns, främst i Design och Prissättning.

**Regler för data**

- En design har en arbetskopia som sparas löpande och skyddas med optimistisk låsning.
- En designversion ändras aldrig. Den skapas när kunden väljer ett AI-förslag, skickar en förfrågan eller när säljaren sparar en revision.
- En offertförfrågan pekar på en bestämd designversion och en fryst prisbild.
- Pengar lagras som decimaltal med valuta, aldrig som flyttal. Mått lagras i meter.
- Alla tider lagras i UTC, och identiteter är UUID version 7.
- Personuppgifter samlas i Identitet och Trädgård, så att radering berör få tabeller.

## Scenmodellen och 3D-editorn

Scendokumentet är systemets viktigaste kontrakt. Editorn, AI:n, prisberäkningen och admin läser och skriver samma format.

**Scendokumentet**

- Ett JSON-dokument med versionsnummer för formatet, så att gamla designer kan uppgraderas.
- Typerna definieras en gång i C#. TypeScript-typer och JSON-schema genereras från dem.
- Koordinater anges i meter i ett lokalt system med känd nordriktning.
- Marken är platt. Kunden kan markera tomten som sluttande, vilket sänker prisets säkerhetsnivå.

| Elementtyp | Geometri | Exempel | Mängd till priset |
| --- | --- | --- | --- |
| Tomt | Polygon | Tomtgräns | Ingen prisrad |
| Byggnad | Polygon med höjd och takform | Hus, garage, förråd | Ingen prisrad |
| Yta | Polygon | Gräsmatta, plattor, grus, trädäck, rabatt | Kvadratmeter |
| Linje | Linje med flera punkter | Häck, staket, kantsten | Meter |
| Punkt | Position och rotation | Träd, buske, kruka, möbel | Antal |

Varje element som kostar något pekar på en artikel i katalogen. Det har också en status: befintligt som behålls, befintligt som tas bort eller nytt.

Scenen sparar dessutom kameravinklar och varifrån varje mått kommer: uppskattat, kundens mått eller verifierat.

**Modellbibliotek**

- Stilen är stiliserad: förenklade former och rena färger, inte fotorealism.
- Omkring 40 typmodeller vid lansering, i formatet GLB. Biblioteket växer efter hand.
- Grunden är [Quaternius Stylized Nature MegaKit](https://quaternius.com/packs/stylizednaturemegakit.html) (CC0) med träd, buskar och blommor.
- Ett trädgårdspaket kompletterar med klippt häck, ros, rabatt och staket, till exempel [det här på CGTrader](https://www.cgtrader.com/3d-models/exterior/landscape/low-poly-and-realistic-garden-plants-and-props) för 14,99 dollar. Licensen kontrolleras för webbanvändning före köp.
- Ytor, häckar, staket, hus och enkla buskar genereras i kod ur scendokumentet och behöver inga modellfiler.
- Bara licenser som tillåter kommersiell användning i en webbapp. Källa och licens sparas för varje modell.
- Varje typmodell har nominell storlek, markfäste och ett tak för antal trianglar.
- En artikel kopplas till en typmodell med skala och färgton.
- Filerna får innehållsbaserade namn och lång cachetid.
- Upprepade växter, till exempel en rad buskar, ritas med instansiering.

**Editorns tillstånd**

- Scendokumentet ligger i ett tillståndslager, och React Three Fiber ritar scenen ur det. Inget tillstånd lever enbart i three.js-objekt.
- Varje ändring är ett kommando: lägg till, flytta, byt ut eller ta bort. Det ger ångra och gör om.
- Arbetskopian sparas en sekund efter senaste ändring.
- Priset hämtas från servern en halv sekund efter senaste ändring.
- "Byt ut" behåller plats och rotation, så att kunden snabbt kan prova alternativ.
- Växter visar sin fullvuxna bredd som en ring på marken. Editorn varnar när de står för tätt.

**Prestandamål (förslag)**

- 60 bilder per sekund med 300 placerade element på en vanlig bärbar dator.
- Första scenen syns inom tre sekunder på bredband.

## AI-flödena

Tre AI-jobb körs i bakgrunden. Inget av dem får skriva till en design förrän resultatet har validerats av vanlig kod.

| Jobb | In | Ut | Validering | Reservväg |
| --- | --- | --- | --- | --- |
| Fototolkning | Foton och tomtens form | Element med typ, ungefärligt läge, storlek och säkerhet. Husets färg och takform. | Schema. Alla lägen ligger inom tomten. | Tom tomt som kunden fyller i |
| Designförslag | Nuläge, kundens önskemål och ett urval ur katalogen | Två till tre förslag med planteringsmoduler per zon | Schema, artikel finns och är i lager, växtzon, avstånd, budget | Kunden designar själv |
| Drömbild | 3D-rendering från vald vinkel, artikellista och kundens foto | En bild, märkt som inspirationsbild | Leverantörens innehållsgranskning | 3D-vyn som den är |

Fototolkningen ger en grov bild av nuläget, inte exakta lägen. Därför visas resultatet alltid som ett förslag som kunden bekräftar eller rättar.

Drömbilden styrs av renderingen från 3D-scenen, så att växterna hamnar där de står i designen.

**Så blir designförslagen giltiga**

Språkmodeller är svaga på exakta koordinater. Därför bestämmer modellen vad och ungefär var, och koden bestämmer exakt var.

1. Kod delar in tomten i namngivna zoner, till exempel "längs södra fasaden" och "vid norra tomtgränsen".
2. Kod väljer ut högst 150 artiklar som är i lager och passar växtzon och ljusläge.
3. Språkmodellen väljer planteringsmoduler per zon, till exempel "perennrabatt, 6 meter, dessa fyra arter".
4. En layoutmotor i C# räknar ut exakta positioner med rätt avstånd.
5. Valideringen körs. Vid fel får modellen felen tillbaka och högst två nya försök.

**Gemensamma regler**

- Varje leverantör ligger bakom ett eget gränssnitt: bildtolkning, designgenerering och bildrendering.
- All utdata följer ett schema. Fritext tolkas aldrig.
- Promptar versionshanteras i kodförrådet.
- Varje jobb loggar promptversion, modell, tid, kostnad och utfall.
- En testmängd med 20 riktiga trädgårdsfoton körs innan en prompt eller modell byts.
- Krav på leverantören: bildindata, strukturerad utdata, personuppgiftsbiträdesavtal och ingen träning på kundernas data.

**Jobbkön**

- API:et sparar jobbet i samma transaktion som övriga ändringar.
- Arbetsprocessen hämtar nästa jobb med radlås (`FOR UPDATE SKIP LOCKED`) och sätter ett lås med tidsgräns.
- Ett jobb vars lås går ut blir tillgängligt igen, så jobb måste gå att köra om utan dubbla effekter.
- Efter tre misslyckanden markeras jobbet som misslyckat och kunden får reservvägen.

## Prissättning och offertflöde

Priset är alltid ett spann, och spannets bredd styrs av hur måtten har kommit till.

**Beräkning**

Uppskattningen är en summa av rader som räknas fram ur scenens mängder.

- **Material:** artikelpris gånger mängd.
- **Arbete:** arbetsnorm per enhet gånger timpris.
- **Borttagning:** arbete för befintliga element som tas bort.
- **Fasta poster:** etablering, transport och bortforsling.
- **Moms:** läggs på sist och visas för sig.

**Spann och säkerhetsnivå**

| Måttkälla | Säkerhetsnivå | Startvärde för spann |
| --- | --- | --- |
| Uppskattat från foto eller karta | Låg | ± 30 % |
| Kundens egna mått | Medel | ± 15 % |
| Säljarens verifierade mått | Hög | ± 5 % |

Procenttalen är mina förslag till startvärden. Företaget ställer in dem i admin och bör justera dem mot verkliga jobb.

Spannet läggs på rader som beror på yta eller längd. Styckvaror som träd och krukor har exakt antal.

**Regler**

- Beräkningen är rena funktioner i modulen Prissättning: mängder, prislista och regler in, uppskattning ut.
- Servern räknar alltid. Klienten visar bara resultatet.
- Varje uppskattning sparar de priser som gällde, så att den kan visas igen oförändrad.
- En offertförfrågan fryser designversion och prisbild. Senare prisändringar i katalogen påverkar inte ärendet.
- Avrundning sker en gång, på totalsumman.
- Kunden ser alltid att priset är ungefärligt och att den bindande offerten kommer från säljaren.

**Offerten**

- Säljaren skapar offerten från uppskattningen som en redigerbar radlista.
- Offerten skickas som PDF med giltighetstid. Digital signering ingår inte i version 1.

## Infrastruktur och drift

Allt körs i Azure, region Sweden Central, med hanterade tjänster och infrastrukturen beskriven som kod.

| Resurs | Används till | Kommentar |
| --- | --- | --- |
| Azure Container Apps | API och arbetsprocess som två containrar | API:et serverar även webbklienten, så allt har samma ursprung |
| Azure Database for PostgreSQL | All data och jobbkön | Automatisk backup med återställning till valfri tidpunkt |
| Azure Blob Storage | Foton, drömbilder och 3D-modeller | Foton är privata med kortlivade länkar. Modeller är publika med lång cachetid. |
| Azure Key Vault | Hemligheter | Åtkomst med hanterad identitet, inga lösenord i konfiguration |
| Azure Container Registry | Containeravbilder | Märkta med commit |
| Application Insights | Loggar, spårning och larm | Via OpenTelemetry |
| E-posttjänst | Engångskoder och aviseringar | Leverantör väljs av byggaren |

**Miljöer**

- **Lokalt:** Aspire startar API, arbetsprocess, PostgreSQL, fillagringsemulator och webbklient med ett kommando.
- **Staging:** samma mall som produktion i minsta storlek.
- **Produktion:** minst en instans av API och arbetsprocess alltid igång.

**Driftsättning**

- Infrastrukturen beskrivs i Bicep i kodförrådet.
- GitHub Actions bygger, testar och driftsätter. Inloggningen mot Azure är federerad, utan sparade hemligheter.
- Varje ändring på huvudgrenen går till staging. Produktion kräver ett manuellt godkännande.
- Databasmigreringar körs som ett eget steg före ny kod och är bakåtkompatibla.
- Återgång görs genom att peka om till föregående avbild.

**Backup och återställning**

- Mål (förslag): högst fem minuters dataförlust och drift igen inom fyra timmar.
- En återställning av databasen provas före lansering.
- Fillagringen har mjuk radering i sju dagar.

**Kostnad**

AI-anropen är den rörliga och sannolikt största kostnaden. Kostnaden per genomfört kundflöde mäts i prototypen och styr kvoterna. Den fasta kostnaden räknas fram i Azures priskalkylator under vecka 1.

**Kodförrådets struktur**

```text
src/
  Api/                  Värd för API och webbklient, sammansättning av moduler
  Worker/               Värd för bakgrundsjobb
  AppHost/              Aspire, lokal miljö
  Modules/
    Garden/             Domain, Features, Infrastructure
    Catalog/
    Design/
    Pricing/
    Leads/
    Ai/
    Identity/
  Contracts/            Gränssnitt och dataobjekt mellan moduler
  SharedKernel/         Pengar, längd, yta, polygon
  web/
    src/app/            Kundflödet
    src/editor/         3D-editorn
    src/admin/          Admin
    src/api/            Genererad API-klient och typer
tests/
  Unit/  Integration/  Architecture/  E2E/
infra/                  Bicep
assets/                 Verktyg för att optimera 3D-modeller
evals/                  Testfoton och förväntade resultat
docs/                   Design, beslut och promptar
```

## Säkerhet och GDPR

Foton av hem och adresser är personuppgifter, så åtkomst, lagringstid och radering byggs in från början.

**Inloggning och åtkomst**

- Kunder loggar in utan lösenord, med engångskod via e-post.
- Personal loggar in med lösenord och tvåfaktor. Rollerna är Säljare och Administratör.
- Sessioner ligger i säkra kakor. Inga inloggningsuppgifter sparas i webbläsarens lagring.
- Varje hanterare kontrollerar att resursen tillhör den inloggade kunden. Regeln har egna tester.
- Delningslänkar är skrivskyddade, omöjliga att gissa och har utgångsdatum.

**Uppladdning**

- Filtypen kontrolleras på innehållet, och ett foto får vara högst 15 MB.
- Varje bild kodas om på servern. Det tar bort GPS-position och annan metadata.
- Kunden uppmanas att undvika personer och registreringsskyltar i bild. Ansikten analyseras aldrig.

**Skydd mot missbruk**

- Anrop begränsas per IP-adress och per användare.
- AI-jobb kräver bekräftad e-post och räknas mot en kvot.
- Säkerhetshuvuden, innehållspolicy och skydd mot förfalskade anrop gäller hela API:et.
- Beroenden och avbilder skannas automatiskt.

**GDPR**

- Företaget är personuppgiftsansvarigt. Avtal behövs med den som driftar, med Azure och med AI-leverantörerna.
- Lagringstid (förslag): designer utan offertförfrågan raderas efter 90 dagars inaktivitet. Ärenden följer företagets policy.
- Admin kan exportera och radera en kunds uppgifter. Radering tar även bort foton och drömbilder.
- En integritetstext visas före första uppladdningen.
- Loggar innehåller identiteter men inga personuppgifter.
- Administrativa åtgärder loggas: prisändringar, statusbyten, export och radering.

Rättslig grund, avtal och lagringstider behöver stämmas av med företagets jurist. Det här avsnittet är teknisk design, inte juridisk rådgivning.

## Kvalitet

Testerna läggs tyngst där fel kostar mest: pris, geometri, validering av AI-utdata och åtkomstkontroll.

| Nivå | Vad som testas | Verktyg |
| --- | --- | --- |
| Enhetstester | Prisregler, yt- och längdberäkning, scenvalidering, layoutmotor | xUnit |
| Integrationstester | API mot riktig PostgreSQL, jobbkön, åtkomstregler | Testcontainers |
| Arkitekturtester | Modulgränser | NetArchTest |
| Flödestest | Hela kundflödet i webbläsare med simulerad AI | Playwright |
| AI-utvärdering | 20 foton med förväntat resultat | Egen körning utanför CI |

AI-leverantörerna ersätts med inspelade svar i alla automatiska tester, så att testerna är snabba, gratis och ger samma resultat varje gång.

**Övervakning**

- Spårning följer ett anrop från API:et genom jobbet till AI-leverantören.
- En tratt visar hur många kunder som når varje steg i flödet.
- Larm går vid hög andel misslyckade jobb, jobb som väntat över två minuter och serverfel.
- Larm går också när AI-kostnaden når 80 procent av dagsbudgeten.

**Kostnadstak för AI**

- Kvot per trädgård (förslag): 3 fototolkningar, 5 designomgångar och 5 drömbilder.
- Hela tjänsten har en dagsbudget. När den nås pausas AI-stegen och kunden kan designa själv.
- Kvoter och budget ställs in i admin.

**Driftegenskaper**

- API och arbetsprocess har hälsokontroller.
- Vid omstart avslutar arbetsprocessen pågående jobb eller lämnar tillbaka dem till kön.
- Varje AI-steg kan stängas av för sig med en inställning.

## Etapper

Arbetet delas i fyra veckor med en grind i slutet av varje, och det mest osäkra provas först.

&#91;embedded content: etapper · 4 veckor, 4 grindar, lansering senast 13 december\]

Varje grind är ett beslut: fortsätt, förenkla eller byt till reservvägen.

| Vecka | Byggs | Grindens fråga |
| --- | --- | --- |
| 1 (16–22 nov) | Tre prototyper: fototolkning på tio riktiga foton, 3D-editor med 300 element och drömbild från rendering. Parallellt kodförråd, CI, staging och inloggning. | Håller kvalitet, kostnad och svarstid, eller ska något förenklas? |
| 2 (23–29 nov) | Scenformat, katalog med Excelimport, editor med katalogpanel, sparning och versioner, prisberäkning med spann. | Kan en människa bygga en design ur lagret och se ett pris? |
| 3 (30 nov–6 dec) | Jobbkö, fototolkning med rättningssteg, zoner och layoutmotor, designförslag, drömbild, offertförfrågan, ärendevy och kvoter. | Fungerar hela flödet i staging med riktiga artiklar? |
| 4 (7–13 dec) | Radering och export, lagringstider, larm, återställningstest, säkerhetsgenomgång, test med tre till fem kunder och en säljare. | Den 10 december: är appen redo för riktiga kunder? |

Prototyperna från vecka 1 är engångskod. De ger svar och mätvärden, och det som behålls byggs om i den riktiga strukturen.

**Stryks i den här ordningen om tiden tryter**

1. Kartdata från Lantmäteriet. Kunden ritar tomten.
2. Flera designförslag. Ett förslag räcker.
3. Säljarens redigering i 3D. Säljaren ser designen och ändrar offertraderna.
4. Drömbilden. Den läggs till efter lansering.

Korrekt pris, åtkomstkontroll, radering av personuppgifter, backup och kostnadstak stryks aldrig.

**Behövs från företaget (förslag på datum)**

| Vad | Senast |
| --- | --- |
| Tio till tjugo riktiga trädgårdsfoton | 18 november |
| Artikellista med priser i importmallen | 27 november |
| Arbetsnormer och fasta poster | 27 november |
| Integritetstext och lagringstider | 4 december |
| Testkunder och en säljare för vecka 4 | 4 december |

## Risker, antaganden och öppna frågor

De två största riskerna är fototolkningens kvalitet och tiden, och båda hanteras genom att prova det svåra först och ha en reservväg för varje AI-steg.

| Risk | Följd | Åtgärd |
| --- | --- | --- |
| Fototolkningen placerar fel | Kunden tappar förtroende i steg 2 | Resultatet visas som förslag att rätta. Tom tomt som reservväg. |
| Språkmodellen ger ogiltiga designer | Inga förslag eller fula förslag | Zoner, planteringsmoduler och layoutmotor. Validering med omförsök. |
| Drömbilden avviker från designen | Kunden förväntar sig något annat än offerten | Bilden styrs av 3D-renderingen och märks som inspirationsbild. |
| En månad räcker inte | Lanseringen försenas | Prototyper vecka 1, grindar varje vecka och en bestämd strykningsordning. |
| Modellbiblioteket försenas | Editorn ser tom eller enformig ut | Quaternius och trädgårdspaketet hämtas vecka 1. Ytor, häckar och hus genereras i kod. |
| Prisdata saknas eller är ofullständig | Priset blir fel eller går inte att visa | Importmall till företaget vecka 1, med en namngiven ansvarig. |
| AI-kostnaden skenar | Oväntad faktura | Kvoter, dagsbudget, larm och avstängning per steg. |
| Ansökan hos Lantmäteriet drar ut | Ingen automatisk tomtform | Kunden ritar tomten. Kartdata kopplas på när tillståndet finns. |
| Sluttande tomter | Priset blir för lågt | Kunden kan markera sluttning, vilket sänker säkerhetsnivån. |
| 3D går trögt på svaga datorer | Kunden ger upp i editorn | Triangeltak, instansiering och ett enklare visningsläge. |

**Mina antaganden, rätta dem som är fel**

- Gränssnittet är på svenska, och priser visas i kronor inklusive moms.
- Systemet byggs för ett företag.
- Lagret innebär sortiment med lagerstatus, inte reservation av exakta saldon.
- Arbetskostnad ingår i det ungefärliga priset, inte bara material.
- Kunden bekräftar sin e-post innan första AI-jobbet.
- Redigering i 3D görs på dator eller surfplatta.
- Marken är platt i scenen.

**Frågor till företaget**

- [ ] Var finns artiklar och priser i dag, och vem tar fram importfilen?
- [ ] Finns arbetsnormer per artikel eller yta, eller behöver de tas fram?
- [ ] Ska skattereduktion som RUT eller ROT visas i priset?
- [ ] Vilka lagringstider vill företaget ha för designer och ärenden?
- [ ] Vem äger Azure-prenumerationen och avtalen med AI-leverantörerna?
- [ ] Vilken månadsbudget finns för drift och AI?
- [ ] Vem ansöker om kartdata hos Lantmäteriet?

**Lämnat till byggaren**

- Visuell design, komponentbibliotek och ton i gränssnittet.
- Val av AI-modeller efter prototypen, inom kraven i avsnittet om AI-flödena.
- Val av e-posttjänst.
- Namn på slutpunkter, tabeller och exakta biblioteksversioner.
