# Fototolkning – design

Delprojekt 8 i [färdplanen](../plans/2026-11-13-roadmap.md). Källan för regler är [docs/design.md](../../design.md):
"Kundens flöde" steg 1–2, "AI-flödena", "Säkerhet och GDPR" (Uppladdning) och kvoterna. Jobben körs i kön från
[delprojekt 7](2026-11-17-ai-jobs-design.md).

## Syfte

Kunden ska slippa bygga upp sin trädgård från noll. Hen laddar upp några foton, visar på tomten var varje foto togs, och
får tillbaka ett **förslag** på vad som finns i trädgården i dag: gräsmatta, rabatter, häckar, träd, uteplats, staket och
husets färg. Kunden godkänner eller tar bort varje sak. Det kunden godkänner blir befintliga element i scenen, som
designen sedan bygger vidare på.

## Vad provet visade (2026-11-18)

Ett slängprov med 5 trädgårdsfoton och Haiku 5.5, Sonnet 5.5 och Opus 5.5 (totalt 3,68 kr):

- Alla tre modellerna hittade de viktiga sakerna och lade dem rätt i förhållande till varandra. Husets färg blev rätt
  varje gång. Alla svar var giltig JSON.
- Opus såg mest detaljer (hushörn, enskilda trampstenar). Sonnet var nästan lika bra och snabbast.
- Skalan var osäker: samma gräsmatta fick olika storlek beroende på modell.
- Varje foto ger en karta sedd *från kameran*. Det svåra är att lägga ihop flera foton på tomten, inte att tolka dem.

Kostnad per foto: Haiku 0,02 kr, Sonnet 0,15 kr, Opus 0,57 kr. Tid per foto: 14, 9 respektive 24 sekunder.

## Vägval

| Väg | Så funkar det | Avvägning |
| --- | --- | --- |
| **A. Kunden pekar ut kameran (vald)** | För varje foto trycker kunden var hen stod på tomten och drar en pil åt det håll kameran pekade. Koden vrider och flyttar varje fotos karta till rätt plats. | Ett extra steg för kunden, några sekunder per foto. AI:n gör det den är bra på, koden räknar lägen. |
| B. Bara en lista | AI:n listar vad som finns, kunden placerar allt själv. | Enklast och aldrig fel läge, men kunden får mest jobb och lite "wow". |
| C. AI:n fogar ihop allt | Alla foton och tomtens form i ett anrop, AI:n gissar var fotona togs. | Minst jobb för kunden, men provet tyder på fel med flera meter. |

*Josef valde A 2026-11-18.*

## Mål

- Kunden laddar upp 1–6 foton till sin trädgård. Servern kontrollerar och kodar om varje foto innan det sparas.
- Kunden markerar för varje foto var kameran stod och åt vilket håll den pekade, på tomten sedd uppifrån.
- Ett fototolkningsjobb i kön tolkar fotona, lägger varje fotos saker på tomten, slår ihop dubbletter och kontrollerar
  resultatet.
- Kunden ser förslaget i 3D-editorn, godkänner eller tar bort varje sak, eller godkänner allt på en gång. Varje
  godkännande är ett kommando som går att ångra.
- Går tolkningen inte, eller blir förslaget tomt, får kunden reservvägen: tomten med huset, där hen lägger in det som
  finns själv.
- Scenformatet kan beskriva befintliga saker utan artikel (ett befintligt träd har ingen artikel i katalogen) och husets
  färger.

## Inte mål

- Kundflödets sex steg som egna sidor, tomtritningen med två mått och kartdata (delprojekt 12). Här byggs uppladdning,
  kameramarkering och granskning som en panel i den befintliga editorn, så att delprojekt 12 kan flytta in dem i flödet.
- En testmängd med 20 foton och ett verktyg som jämför promptar (design.md, "Gemensamma regler"). Provscriptet från
  2026-11-18 räcker tills vi har fler riktiga foton. Ska byggas innan prompten eller modellen byts efter lansering.
- Att tolka sluttning, jordmån, ljusläge eller växtarter. Bara form och sort av sak (lövträd, barrträd, buske …).
- Valmtak och andra takformer. AI:ns "valmtak" blir sadeltak (`gable`) tills modellbiblioteket har fler tak.
- Kravet på bekräftad e-post (väntar på e-posttjänsten, Josefs beslut 2026-11-17).

## Det som inte får hända

- Att AI:ns svar hamnar i en design utan att vanlig kod har kontrollerat det, eller utan att kunden har godkänt det.
- Att ett foto sparas med GPS-position eller annan metadata, eller att originalfilen sparas alls.
- Att en kund ser en annan kunds foton eller tolkning.
- Att kunden fastnar: varje fel leder till reservvägen med en förklaring på svenska.
- Att samma gräsmatta eller häck kommer med två gånger för att den syns på två foton.

## Hur det fungerar

### 1. Uppladdning

- `POST /api/gardens/{gardenId}/photos`, en fil per anrop. Kunden måste äga trädgården.
- Högst 6 foton per trädgård och högst 15 MB per fil. Filtypen avgörs av innehållet, inte filnamnet. JPEG, PNG, WebP
  och HEIC (iPhone) tas emot.
- Servern kodar om varje foto till JPEG, längsta sida högst 1568 px (det Claude ändå skalar ner till), roterar efter
  EXIF-orienteringen och tar bort all metadata. Bara den omkodade bilden sparas, i Blob Storage i en privat behållare.
  *Mitt val: Magick.NET (Apache 2.0), eftersom det läser HEIC. ImageSharp kräver betald licens för företag över en
  viss omsättning och SkiaSharp läser inte HEIC på Linux.*
- `GET` listar fotona med en kortlivad länk (SAS, 10 minuter) för visning. `DELETE` tar bort ett foto.
- Fotona ägs av Garden-modulen. Ai-modulen hämtar bildbytes genom ett gränssnitt i Contracts
  (`IGardenPhotos.ReadAsync`), aldrig direkt ur lagringen.

### 2. Kamerans plats

- För varje foto sparar kunden en **kamerapose**: punkt på tomten (meter, scenens koordinater) och riktning (grader,
  0 = norr, medurs). Lagras på fotot. `PUT /api/gardens/{gardenId}/photos/{photoId}/camera`.
- I editorn: kunden väljer ett foto i panelen, editorn växlar till vy uppifrån, kunden trycker där hen stod och drar
  åt det håll kameran pekade. Fotot visas som miniatyr bredvid så att kunden ser vad som ska pekas ut.
- Kameran måste stå inom tomten eller högst 5 m utanför (man kan fotografera från gatan).

### 2b. Forma tomten och huset

Kameramarkeringen blir bara rätt om tomten och huset liknar verkligheten, och många tomter har inga raka linjer.
Därför kan kunden forma hela tomten, inte bara flytta hörn. *Josefs beslut 2026-11-18.*

- **Rita om:** kunden klickar ut tomtgränsen punkt för punkt i vyn uppifrån och avslutar genom att klicka på första
  punkten. Samma för huset.
- **Flytta punkter:** varje punkt på gränsen har ett handtag som går att dra.
- **Lägga till punkter:** mitt på varje sträcka finns ett mindre handtag. Drar kunden i det blir det en ny punkt, så att
  en rak sträcka kan böjas. Många punkter ger en mjuk kurva; scenformatet lagrar fortfarande bara punkter.
- **Ta bort punkter:** dubbelklick på en punkt. Tomten har minst 3 punkter.
- **Mått:** längden på varje sträcka visas i meter medan kunden drar, så att den kan jämföras med verkligheten.
- Varje ändring är ett kommando (`move-point`, `insert-point`, `remove-point`, `redraw`) och går att ångra.
- Gränsen får inte korsa sig själv, och huset måste ligga inom tomten. Editorn säger till på svenska och behåller den
  senaste giltiga formen.
- Delprojekt 12 lägger till de två måtten för skalan och kartdata. Formverktyget här återanvänds där.

### 3. Tolkningen (Worker)

- "Tolka foton" köar ett jobb av sorten `photo-interpretation` med trädgårdens id, designens id och
  fotonas id. Alla foton måste ha en kamerapose. Kvoten (3 per trädgård) och dagsbudgeten gäller som i delprojekt 7.
- Handlaren hämtar fotona och tomtens och husets form ur designens arbetskopia (via ett gränssnitt i Contracts).
- Ett anrop per foto, med samma sorts fråga som i provet: en karta sedd uppifrån med kameran i origo, `y` framåt,
  `x` åt höger, i meter, med typ, form (yta, linje, punkt), punkter, bredd, höjd och säkerhet, plus husets väggfärg,
  material, takform och takfärg. Svaret tas emot som strukturerad utdata (JSON-schema i anropet), inte som fritext.
- Prompten ligger versionerad i koden, `photo-interpretation@1`. Modellen ställs in per jobbsort:
  `Anthropic:Models:photo-interpretation`. *Mitt val: Sonnet 5.5 som standard; bra nog i provet, 0,15 kr och snabbast.
  Opus är ett byte i inställningarna om kunderna behöver mer detalj.*
- Anropen för de olika fotona görs efter varandra inom samma jobb. Ett nytt försök gör om alla. Ett foto vars svar inte
  går att tolka hoppas över och nämns i resultatet; jobbet misslyckas bara om inget foto gav något.

### 4. Från foto till tomt (vanlig kod, i Ai-modulen)

1. **Flytta och vrida.** Varje punkt förs från kamerans koordinater till scenens med kameraposen.
2. **Kasta och klippa.** Saker som ligger helt utanför tomten (med 0,5 m tolerans) kastas. Ytor klipps mot tomten.
   Saker inne i huset kastas. Säkerhet under 0,3 kastas.
3. **Slå ihop dubbletter.** Två saker av samma sort slås ihop om de överlappar: ytor när överlappet är minst 30 % av den
   mindre ytan, linjer när de ligger inom 1,5 m från varandra längs minst halva den kortare, punkter när avståndet är
   mindre än halva den största bredden. Den sammanslagna saken får medelvärdet av lägena och den högsta säkerheten.
   *Mina tal; justeras efter riktiga foton.*
4. **Gräsmattan.** Överlappande gräsytor från olika foton slås ihop till en yta.
5. **Huset.** Färger och takform tas från det foto med högst säkerhet för huset. Husets form ändras inte; den kommer
   från kunden.
6. **Översätta till scenen.** AI:ns sorter blir scenelement:

| AI-sort | Scenelement |
| --- | --- |
| lawn, flowerbed, gravel, patio, path | `surface` med `material` grass, bed, gravel, paving, paving |
| hedge, fence | `line` med `form` hedge respektive fence |
| tree | `point` med `form` tree-large, tree-small eller pine efter höjd och om det är barrträd |
| shrub | `point` med `form` shrub-round |
| raised-bed, shed, edging, other | kastas i den här versionen och nämns i "osäker på" |

7. **Kontrollera.** Resultatet körs genom samma scenkontroll som en sparad design (`ISceneCheck`), med de nya reglerna
   nedan. Bara element som klarar kontrollen kommer med.

Resultatet sparas på jobbet: en lista med föreslagna element (scenformatet, status `existing-keep`, måttkälla
`estimated`), husets färger och takform, vad som kastades och varför, och kostnaden.

### 5. Granska förslaget (editorn)

- Klienten frågar efter jobbets status varannan sekund (`GET /api/gardens/{gardenId}/photo-interpretations/{jobId}`,
  bara ägaren). Medan den väntar visas "Tolkar dina foton …".
- Förslaget ritas i 3D som halvgenomskinliga element ovanpå scenen. En lista visar varje sak med namn och säkerhet och
  knapparna "Ta med" och "Ta bort", plus "Ta med alla".
- "Ta med" är ett kommando (`add`), så det går att ångra. Husets färg och tak är ett eget förslag med "Ta med".
- Förslagen sparas inte i designen förrän kunden tagit med dem. Lämnar kunden sidan kan förslaget hämtas igen från
  jobbet.
- Misslyckas jobbet, eller blir förslaget tomt: "Vi kunde inte tolka fotona. Lägg in det som finns i trädgården själv,
  det går snabbt." Scenen har redan tomten och huset; det är reservvägen.

### 6. Ändringar i scenformatet (formatVersion 2)

- `PointElement` får `form` (formnyckel, till exempel `tree-large`) och `width` och `height` i meter. De används när
  `articleId` saknas, alltså för befintliga saker. Editorn ritar då formen i den storleken.
- `LineElement` får `form` (`hedge` eller `fence`) och `height`, på samma sätt.
- `BuildingElement` får `wallColor` och `roofColor` (hex, till exempel `#E3B23C`). Saknas de ritas huset som i dag.
- Ett element utan artikel måste ha en form, annars är scenen ogiltig. Element utan artikel prissätts inte (oförändrat).
- Gamla dokument (version 1) uppgraderas utan ändring av innehållet; fälten är tomma.
- TypeScript-typerna och JSON-schemat genereras om (`npm --prefix src/web run gen:scene`).

### 7. Kontrollen utökas (från färdplanen)

- Alla lägen ska ligga inom tomten: ytor, linjer och byggnader också, inte bara punkter. Tolerans 0,5 m, eftersom
  staket och häckar står på tomtgränsen.
- "Byt" på ett befintligt element lämnar det gamla på samma plats med status `existing-remove`. Klick väljer då det
  nya, borttagna växter räknas inte i varningen för tät plantering, och det borttagna ritas genomskinligt. Efter
  "Byt" och "Ångra" blir det återställda elementet valt igen.

## Säkerhet och GDPR

- Varje anrop kontrollerar att trädgården tillhör den inloggade kunden. Egen test per endpoint, som i delprojekt 4.
- Fotona ligger i en privat behållare. Länkar gäller i 10 minuter och bara för ett foto.
- Fotona och AI-svaren raderas när trädgården raderas. Lagringstiden i övrigt följer designen (delprojekt 13).
- Fotot skickas till Claude och sparas inte där (Anthropics villkor för API:t: ingen träning på kunddata).

## Test

- Enhetstester för koden i steg 4: flytta och vrida, klippa mot tomten, slå ihop dubbletter, översätta sorter,
  med påhittade AI-svar. Det är här de flesta fel kan uppstå.
- Integrationstest för uppladdningen: omkodning tar bort EXIF och GPS, för stora och falska filer nekas, gränsen 6
  foton, annan kunds trädgård ger 404.
- Handlaren testas med en låtsasmodell som returnerar sparade svar från provet.
- Editorn: tester för granskningslistan (ta med, ta bort, ta med alla, ångra) och för "Byt" på samma plats.
- Ett riktigt anrop mot Claude med dina foton körs för hand innan delprojektet är klart, efter att du sagt ja.

## Svar på granskarens frågor

- **Tomten och huset.** I dag startar varje kund från demoträdgården (`demoScene.json`, tomt 24 × 18 m, hus 10 × 6 m)
  och sparar vid första ändringen. Kunden formar sin riktiga tomt och sitt hus med verktyget i avsnitt 2b, och
  tolkningen läser dem ur designens arbetskopia.
- **Klippning mot tomten.** Ny beroende: Clipper2 (Boost-licens, fri för företag) för att klippa och jämföra ytor.
  *Mitt val.*
- **Om scenen ändras innan kunden granskat.** Förslaget läggs till som det är. Varje sparning går redan genom
  scenkontrollen, så ett förslag som inte längre ryms på tomten nekas där; editorn tar då bort det ur listan med
  "Passar inte längre på tomten." *Mitt val.*
- **"Byt".** Kommandot `replace` i `src/web/src/editor/sceneStore.ts` (anropas från `selectionCommands.ts`). Gäller
  också befintliga element utan artikel, som AI:ns träd. Prissättningen räknar redan `existing-remove` som borttagning
  (delprojekt 5) och ändras inte.
- **Formatversion 2.** `SceneJson` och `SceneValidator` godtar i dag bara version 1. De ska läsa både 1 och 2, uppgradera
  version 1 när den läses och spara som version 2. Inget skrivs om i databasen i förväg. Inga element utan artikel
  finns i sparade designer utom ytor (gräsmattan), som redan har `material` och inte behöver `form`.
- **Tid och kostnad per jobb.** Sex anrop efter varandra tar högst cirka 2,5 minuter med Opus, inom låset på 5 minuter.
  Varje anrop har redan en gräns på 2 minuter. Varje försök räknas mot dagsbudgeten (varje anrop loggas). Ett
  misslyckat jobb räknas inte mot kvoten (så fungerar kön redan).
- **Foton som tas bort.** Jobbet tar med fotonas id när det köas. Ett foto som tagits bort när jobbet körs hoppas över.
  Ett färdigt förslag ligger kvar. Gränsen 6 räknar foton, inte jobb.
- **HEIC och lagring lokalt.** Ett integrationstest med en riktig HEIC-fil avgör om Magick.NET-paketet läser HEIC i
  Linux-containern i CI. Gör det inte det tas HEIC bort ur listan och kunden får "Spara fotot som JPEG". Lokalt och i
  tester används Azurite (finns redan i AppHost; i tester via Testcontainers).
- **Vy uppifrån.** Finns inte i editorn i dag och byggs här.

## Kvar för den som bygger

- Hur kameramarkeringen ser ut och känns i editorn, så länge den följer punkt 2.
- Exakt promptformulering, så länge svaret följer schemat och versionen höjs vid ändring.
- Om bildbehandlingen körs i API:t direkt vid uppladdning (förslaget) eller som ett eget steg.
