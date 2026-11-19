# Plantskolan – designsystem för hela webbappen

Ett delprojekt utanför färdplanens nummer, infört 2026-11-19 före delprojekt 10. Produktens sanning står i
[docs/design.md](../../design.md) och, för designarbete, i [PRODUCT.md](../../../PRODUCT.md).

Förebild: [skissen av editorn](assets/2026-11-19-plantskolan-editor.png) (källa:
[HTML](assets/2026-11-19-plantskolan-editor.html), 1440 × 900). Skissen är ett mål för känsla och finish, inte en
pixelritning. Där skissen och den här texten skiljer sig gäller texten.

## Syfte

Appen ser i dag ostylad ut: systemtypsnitt, vanliga webbläsarknappar och en rad knappar i editorn. Josef vill att
Drömträdgård ska se genomarbetad ut och inte AI-gjord. Det ska kännas som ett premiumverktyg som en villaägare
litar på när hen planerar sin trädgård för 50 000–100 000 kr.

Josef valde 2026-11-19 stilen **Plantskolan** bland sju skisser i två omgångar. Den första omgången, med platta och
kantiga skisser, underkände han som "2010".

## Mål

- Ett designsystem i koden: tokens (färger, typsnitt, avstånd, hörnradier och skuggor), ikoner och ett tiotal
  komponenter som alla sidor använder.
- **Editorn** görs om efter skissen:
  - granfärgad ram;
  - 3D-scenen som ett rundat fönster i varmt ljus;
  - verktygen i en svävande skena till vänster;
  - det valda elementet som en växtetikett till höger;
  - katalogen som en hylla längs nederkanten;
  - priset i sidhuvudet.
- **3D-scenen** får Plantskolans ljus: varm bakgrund, gyllene solljus och mjukare skuggor. Markeringen får
  Plantskolans terrakotta.
- **Admin, personalinloggningen och startsidan** görs om i samma värld men lugnare, som arbetsytor.
- `DESIGN.md` i roten beskriver det färdiga systemet så att framtida sidor följer det. Filen skrivs när bygget är
  klart, av Impeccables dokumenterare.

## Icke-mål

- Kundflödets sex steg som riktiga sidor (delprojekt 12). Stegraden i skissen byggs inte nu.
- Offertförfrågan (delprojekt 11). Knappen "Begär offert" byggs inte nu.
- Mörkt läge.
- Redigering på mobil. Editorn är för dator, som i dag.
- Nya funktioner i editorn, utom två som Josef valde 2026-11-19: **sök i katalogen** (filtrerar hyllans kort på
  namn och sort, utan skillnad på stora och små bokstäver) och **Ovanifrån/3D** som kunden själv kan byta (låst till
  ovanifrån medan tomten formas eller foton markeras). Allt annat som finns i dag finns kvar och fungerar likadant.
- Nya 3D-modeller. Scenens modeller är desamma; bara ljus, bakgrund och markering ändras.

## Anti-mål

- Ett utseende som ser AI-gjort eller daterat ut. Undvik:
  - tjocka ramar;
  - platta grå paneler;
  - standardknappar;
  - kräm och serif;
  - lila gradienter.
- Ett pris som ser mer exakt ut än det är. Spannet och "uppskattade mått" ska alltid synas.
- Knappar som inte gör något. Det som inte finns byggs inte som attrapp.
- Sämre tillgänglighet än i dag. All text ska klara WCAG AA i kontrast och allt ska gå att nå med tangentbordet.
- Data som skickas till tredje part för att visa typsnitt eller ikoner.

## Vägval

| Väg | Så funkar det | Avvägning |
| --- | --- | --- |
| **A. Egen CSS med tokens och egna komponenter (vald)** | CSS-variabler i en tokens-fil, ett tiotal React-komponenter i `src/web/src/ui/`, vanliga CSS-filer som i dag. | Exakt Plantskolan, inget nytt verktyg. Lite mer egen CSS. |
| B. Tailwind | Styling med klassnamn direkt i JSX. | Snabbt för den som kan det, men all styling skrivs om och JSX blir svårläst för en nybörjare. |
| C. Komponentbibliotek (shadcn, MUI) | Färdiga komponenter som stylas om. | Bra tillgänglighet, men bibliotekens eget utseende är det "AI-utseende" som ska bort. |

*Josef valde A och "hela appen" 2026-11-19.*

## Designsystemet

### Tokens (`src/web/src/ui/tokens.css`)

Värdena kommer från skissen. Namnen är förslag; byggaren får byta namn men inte värden utan skäl.

| Roll | Token | Värde |
| --- | --- | --- |
| Ram (gran) | `--fir`, `--fir-2`, `--fir-3` | `#1C392D`, `#244536`, `#2F5241` |
| Text på ram | `--on-fir`, `--on-fir-2`, `--on-fir-3` | `#F3EEE4`, `#C3CEC2`, `#97A99B` |
| Ytor (linne) | `--linen`, `--linen-2`, `--linen-3` | `#F8F4EC`, `#EFE9DE`, `#E4DBCC` |
| Text | `--ink`, `--ink-2`, `--ink-3` | `#1E2923`, `#48544D`, `#626C65` |
| Accent (terrakotta) | `--terra`, `--terra-ink`, `--terra-soft` | `#BC522B`, `#9C3F1E`, `#F6E2D6` |
| Tas bort | `--cut` | `#2A2420` |
| Scenens bakgrund | `--scene-top`, `--scene-mid`, `--scene-bottom` | `#EDE3D3`, `#E2D5C1`, `#D5C4AA` |
| Lyft (skugga) | `--lift` | `0 1px 2px rgba(40,30,15,.08), 0 6px 16px -6px rgba(40,30,15,.16), 0 18px 40px -16px rgba(40,30,15,.24)` |
| Lyft på mörkt | `--lift-dark` | `0 1px 2px rgba(0,0,0,.25), 0 12px 32px -12px rgba(0,0,0,.5)` |
| Hörn | `--radius-s`, `--radius-m`, `--radius-l`, `--radius-xl` | 8, 12, 18, 22 px |
| Avstånd | `--space-1` … `--space-8` | 4-px steg: 4, 8, 12, 16, 20, 24, 32, 40 |

Två tillstånd saknas i skissen och läggs till som byggarens val:

- **Varning:** en bärnstensgul ton, för "växter står för tätt" och "saknas i katalogen".
- **Fel:** en röd ton, för felrutor.

Båda ska klara AA mot linne och ligga i samma dova register som paletten.

### Typsnitt

**Familjen Grotesk**, variabel, självhostad via npm-paketet `@fontsource-variable/familjen-grotesk` (OFL-1.1). Inga
anrop till Google: Google Fonts skickar besökarens IP-adress till Google, vilket tyska domstolar har underkänt enligt
GDPR.

Skala: 11,5 / 12,5 / 13,5 / 14 / 16 / 19 / 22 px. Hela kronor och mått sätts med `font-variant-numeric: tabular-nums`.
Tunna mellanrum i "48 600 – 71 200 kr" kommer från `Intl.NumberFormat('sv-SE')`, som i dag.

### Ikoner

**lucide-react** (ISC), 1,75–2 px linje, 16–21 px. Bara de ikoner som används importeras.

### Komponenter (`src/web/src/ui/`)

Ett tiotal små komponenter som alla sidor använder:

- `Button` med varianterna primär, sekundär, lugn och fara.
- `IconButton`.
- `SegmentedControl` för Ovanifrån/3D.
- `Tabs` för Katalog/Foton/Förslag.
- `SearchField`.
- `Chip`, för filter.
- `Card`, en linneyta med lyft.
- `Badge`, till exempel "Ny i designen" och "Slut i lager".
- `Field`, etikett plus inmatning, för formulär i Förslag, admin och inloggning.
- `Notice`, för info, varning och fel.

Krav på komponenterna:

- Varje komponent har synligt fokus (terrakottafärgad ring) och tydliga tillstånd för hover, aktiv och avstängd.
- Komponenterna använder bara tokens, aldrig egna färgvärden.

## Sidorna

### Editorn

Placering enligt skissen:

- **Sidhuvud på granram:**
  - ordmärket "Drömträdgård", adress och sparstatus till vänster;
  - prisspannet till höger, med "Ungefärligt pris" och "inkl. moms · uppskattade mått".
  - Där skissen har stegraden och "Begär offert" lämnas plats tills delprojekt 11 och 12.
- **Prisdetaljerna** i dagens prispanel finns kvar och nås genom att klicka på priset, som öppnar ett kort under
  sidhuvudet: säkerhet, delsummor, varav moms, saknade artiklar, "Visa alla rader" och "Priset är ungefärligt…".
  Laddning och fel visas i sidhuvudet som i dag: blekt gammalt pris medan ett nytt räknas, och felmeddelanden.
- **Scenfönstret:** ett rundat fönster (22 px hörn) med scenens varma bakgrund.
- **Verktygsskenan** svävar uppe till vänster i fönstret. Den får dagens åtgärder som ikonknappar med text under:
  - Välj, Forma tomt och Forma hus;
  - avdelare;
  - Ångra och Gör om.
  - Skissens "Rita" och "Nål" finns i dag bara inne i formning och fotomarkering. De visas där de hör hemma, inte i
    skenan.
- **Formningsläget** visar sin instruktionstext, "Rita om" och "Klar" i en svävande remsa uppe i mitten av fönstret.
- **Vyväljaren Ovanifrån/3D** sitter bredvid skenan.
- **Det valda elementet** visas som en växtetikett uppe till höger:
  - namn, latinskt namn när det finns, fakta med prickade rader, pris;
  - Vrid (två håll), Byt och Ta bort;
  - "Byt mot"-listan;
  - för ett element som tas bort: "Behåll".
  - Utan val syns etiketten inte.
  - Hjälptexten ("Klicka på ett element för att välja det…") visas lågt i fönstret.
- **Statusraden** visas nere i fönstret: antal element, antal markerade för borttagning, varning för täta växter,
  saknade artiklar och meddelanden.
- **Hyllan** längs nederkanten (156 px hög):
  - Katalog/Foton/Förslag-flikar, sökfält och kategorichips till vänster;
  - artikelkort i en vågrät rad som går att skrolla: bild, namn, sort eller storlek, pris och "Lägg till";
  - det aktiva placeringsläget visas på kortet;
  - slut i lager visas som en badge.
  - Sorter med flera storlekar visas som ett kort per storlek med sortnamnet överst.
- **Foton och Förslag** innehåller formulär som inte ryms i hyllan. När de väljs öppnas de som en högre panel
  (cirka 380 px bred) till vänster i scenfönstret, intill skenan. Hyllan visar då bara flikarna.
- **Utvecklarverktygen** flyttas in i en liten meny bakom en ikonknapp i statusraden: "Lägg till 300 växter", "Rensa
  nya växter", "Mät prestanda" (med fps-raden) och "Detaljerade modeller".
- **Sparkonflikt och laddning** visas som kort mitt i fönstret.

Kortbilder: varje artikel ritas med sin 3D-modell och färg. Byggaren väljer hur. Ett förslag är att rita varje
unik kombination av modell och färg en gång i en liten dold WebGL-yta, och spara bilden i minnet. Tills bilden finns
visas en färgprick som i dag.

### 3D-scenen

- **Bakgrunden** blir Plantskolans varma gradient, eller en tonad himmel som ligger så nära den som three.js tillåter.
- **Ljuset:**
  - halvklotsljuset blir varmare;
  - solen blir gyllene och låg;
  - skuggorna blir mjuka (PCF soft).
  - Målet är skissens eftermiddagsljus, men modellerna ska fortfarande gå att känna igen och färgerna vara sanna mot
    artiklarna.
- **Markeringen** byts från gult till terrakotta.
- **Befintligt som tas bort** fortsätter att visas som i dag, med en etikett i `--cut` "Tas bort" om den går att visa
  utan att störa.
- **Prestandakravet står kvar:** 60 bilder per sekund med 300 element (design.md). Mjukare skuggor får inte sänka det.

### Admin, personalinloggning och startsida

- **Admin:**
  - granfärgat sidhuvud med "Admin", inloggad användare och "Logga ut";
  - innehållet på linneytor (`Card`);
  - AI-panelens inställningar och budget med `Field`, `Button` och `Notice`.
- **Personalinloggningen:** ett centrerat kort på granfärgad bakgrund. Alla steg, även QR-kod, nyckel och
  reservkoder, med komponenterna.
- **Startsidan:** granfärgad med ordmärket, de sex stegen som en enkel lista och länkarna till editorn och admin. Det
  är fortfarande en utvecklarsida tills kundflödet byggs i delprojekt 12, och den ska inte se ut som en
  marknadsföringssida.

## Tillgänglighet

- Kontrast enligt WCAG AA för all text, även på granram och i badges.
- Synligt fokus på allt klickbart. Flikar och segmentväljare går att styra med piltangenter.
- Knappar som bara är ikoner har `aria-label` på svenska.
- Rörelse respekterar `prefers-reduced-motion`.
- Dagens kortkommandon (Ctrl+Z, Delete, Q, E, Esc) fungerar som förut.

## Verifiering

- **Befintliga tester** (vitest och .NET) ska vara gröna. Testerna som letar efter knapptexter uppdateras bara om
  texten flyttas, aldrig så att ett beteende slutar testas.
- **Nya komponenttester** för det som har beteende:
  - flikar och segmentväljare med tangentbord;
  - prisdetaljer som öppnas och stängs;
  - utvecklarmenyn;
  - att växtetiketten visar "Behåll" för ett element som tas bort.
- **Granskning i webbläsaren** av varje sida i 1440 × 900 och 1280 × 800. Login och admin också i mobilbredd.
- **Impeccables detektor** (`impeccable detect`) körs på de ändrade filerna. Därefter granskar Impeccables
  finish-granskare skärmbilderna mot skissen och kontraktet nedan.
- **Prestanda:** fps mäts med "Mät prestanda" och 300 växter före och efter. Siffrorna skrivs i slutrapporten.

## Impeccables kontrakt

Riktningen sparas som en *surface brief* för editorn (`impeccable surface-brief write`), med Josefs val som pinnad
riktning (`seed: none (pinned)`, "C Plantskolan"). När bygget är klart skriver Impeccables dokumenterare `DESIGN.md`
i roten och `.impeccable/design.json`.

Obs: på Windows skiljer filsystemet inte på stora och små bokstäver. Impeccable läser i dag `docs/design.md` som om
det vore designsystemet. När `DESIGN.md` finns i roten ska det kontrolleras att Impeccable läser rätt fil.

## Beslut efter granskningen

En byggare läste dokumentet och ställde frågor. De här besluten är mina (Claude) och kan rättas av Josef:

- **Adress i sidhuvudet:** designen har ingen adress i dag. Sidhuvudet visar ordmärket och sparstatus; adressen kommer
  med kundflödet (delprojekt 12).
- **Latinskt namn:** artiklarna har inget sådant fält. Det utgår.
- **Fakta i växtetiketten,** de som finns för artikeln, i ordningen:
  - Växtzon;
  - Höjd vid plantering;
  - Fullvuxen höjd;
  - Fullvuxen bredd;
  - Läge (sol, halvskugga, skugga).
  - För ytor och linjer visas mått i stället, när de finns.
- **Pris i etiketten:** artikelns pris med enhet ("3 450 kr", "420 kr/m", "640 kr/m²").
  - Ett befintligt element utan artikel får ingen prisrad.
  - En yta i hyllan har ingen "Lägg till" (som i dag), bara texten "Välj en yta i trädgården för att byta".
- **Byt:** knappen "Byt" i etiketten fäller ut "Byt mot"-listan, som är hopfälld från början. Varje rad har sin
  egen "Byt" som i dag. Listan skrollar inuti etiketten om den är lång.
- **Kategorichips** ersätter dagens kategorirubriker: ett chip per kategori plus "Alla", och hyllan visar korten för
  det valda. Det är dagens gruppering i ny form, inte en ny funktion.
- **Prisdetaljerna:**
  - Kortet stängs med Esc, med ett klick utanför och med ett nytt klick på priset.
  - Esc stänger kortet först, innan det rensar val och verktyg.
  - "Visa alla rader" fäller ut raderna inne i kortet, som skrollar.
  - Priset i sidhuvudet har fast minsta bredd och flyttas aldrig av formningsremsan, som ligger i scenfönstret.
- **Komponenttester:** testmiljön får `@testing-library/react` och en DOM-miljö (jsdom eller happy-dom) som
  utvecklingsberoenden.
- **Paket:** `@fontsource-variable/familjen-grotesk` och `lucide-react` paketeras av Vite i webbappen. Inget hämtas från
  en extern server när sidan körs.
- **Kortbilder:**
  - De ritas lat, först när kortet syns.
  - De ritas i en egen liten renderare, inte i scenens.
  - Varje unik kombination av modell och färg ritas en gång och sparas i minnet.
  - Scenens 60 bilder per sekund mäts med hyllan öppen.

## Kvar åt byggaren

- Exakta komponent-API:er, filnamn inom `src/web/src/ui/` och hur dagens CSS-filer delas upp.
- Hur kortbilderna ritas (se ovan) och hur hyllan skrollar.
- Ljusets exakta värden i scenen, inom målet ovan.
- Varnings- och feltonerna.
