# Jobbkö och AI-ramverk – design

Delprojekt 7 i [färdplanen](../plans/2026-11-13-roadmap.md). Källan för regler är [docs/design.md](../../design.md),
avsnitten "Systemarkitektur", "AI-flödena" (Gemensamma regler, Jobbkön) och kvoterna under "Drift och kostnad".

## Syfte

Alla AI-steg i appen (fototolkning, designförslag och drömbild) är långsamma, kostar pengar och kan misslyckas. Det här
delprojektet bygger maskinrummet de körs i: en kö där jobben väntar, Worker som kör dem, omförsök, en kostnadslogg,
kvoter per trädgård och en dagsbudget. Delprojekt 8, 9 och 10 blir sedan nya sorters jobb som kopplas in.

För att kunna prova maskinrummet innan de riktiga jobben finns får admin ett **testjobb**: en knapp som skickar en kort
fråga till Claude och visar svaret, tiden och kostnaden.

## Mål

- En jobbtabell i PostgreSQL som Worker hämtar från med radlås, ett lås med tidsgräns och högst tre försök.
- Varje AI-anrop loggas med jobb, promptversion, modell, tid, tokens, kostnad i kronor och utfall.
- Kvoter per trädgård och sort av jobb, och en dagsbudget för hela tjänsten. Båda ställs in i admin.
- Claude anropas med Anthropics officiella C#-bibliotek bakom ett eget gränssnitt, så att fler leverantörer kan
  kopplas in.
- Ett testjobb i admin som visar att nyckel, kö, anrop och kostnadslogg fungerar.

## Inte mål

- Fototolkning, designförslag och drömbild (delprojekt 8–10). Här finns bara testjobbet.
- Gemini, bildmodeller och valet av leverantör. Gränssnittet ska bara göra dem möjliga.
- Kravet på bekräftad e-post före AI-jobb ([design.md:417](../../design.md)). Det väntar tills e-posttjänsten är vald.
  Under tiden skyddar kvoterna och dagsbudgeten. *Josefs beslut 2026-11-17.*
- Larm till drift när kostnaden når 80 procent av dagsbudgeten. Larm byggs i delprojekt 14; här loggas en varning.
- En kötjänst (Azure Service Bus) och att servern skickar händelser till klienten. Klienten frågar efter status.

## Det som inte får hända

- Att samma jobb körs av två Workers samtidigt, eller att ett jobb fastnar för alltid när en Worker dör.
- Att AI-svar skrivs in i en design utan att vanlig kod har kontrollerat dem först. (Gäller jobben i 8–10; testjobbet
  skriver inget.)
- Att API-nyckeln finns i API:t, i webbläsaren, i loggar eller i git. Bara Worker läser den.
- Att kostnaden fortsätter växa när dagsbudgeten är nådd.

## Vägval

| | Vad | För | Emot | Valt |
| --- | --- | --- | --- | --- |
| **A** | Egen jobbtabell i AI-modulen, som design.md beskriver | Full kontroll över kvot, budget och kostnad per jobb. Inga extra tabeller eller tjänster. Lätt att testa. | Kön skrivs själv, cirka 200 rader. | **Ja** |
| B | Köbibliotek (Hangfire på PostgreSQL) | Omförsök och översiktssida ingår. | Egna tabeller och format. Kvot, budget och kostnad måste ändå byggas runt det. Går emot designens val. | Nej |

Josef valde A.

## Jobben

Tabellen `ai.jobs`, en rad per jobb:

- `id` (UUID v7), `kind` (till exempel `connection-test`), `status`, `input` (jsonb), `result` (jsonb, tomt tills
  klart), `failure` (kort orsak på svenska när jobbet misslyckas).
- `garden_id` och `customer_id`: vems jobbet är. Tomma för testjobbet, som startas av personal.
- `requested_by_staff_id`: personen som startade ett testjobb.
- `attempts`, `locked_until`, `next_attempt_at`, `created_at`, `completed_at` (UTC).

Status: `queued` → `running` → `succeeded` eller `failed`.

**Så hämtar Worker ett jobb**

1. I en transaktion: välj det äldsta jobbet som är `queued` med `next_attempt_at` passerat, eller `running` med
   `locked_until` passerat, `FOR UPDATE SKIP LOCKED`, `LIMIT 1`.
2. Sätt `status = running`, öka `attempts` och sätt `locked_until` till nu + 5 minuter. Bekräfta transaktionen.
3. Kör jobbet utanför transaktionen.
4. Lyckat: spara resultatet, `succeeded`, `completed_at`.
5. Misslyckat och färre än tre försök: tillbaka till `queued` med `next_attempt_at` om 10 sekunder efter första och
   60 sekunder efter andra försöket.
6. Misslyckat tredje gången, eller ett fel som inte blir bättre av ett nytt försök (till exempel att nyckeln saknas):
   `failed` med orsak. Kunden får reservvägen.

Ett jobb vars lås går ut (Worker dog) tas av nästa hämtning. Därför ska varje sorts jobb tåla att köras om: ett nytt
försök skriver över resultatet, det lägger aldrig till något en gång till.

Worker tittar efter jobb varannan sekund, som skelettet redan gör, och kör ett jobb i taget. Flera Workers kan köras
samtidigt tack vare radlåset.

**Jobb mellan moduler.** Andra moduler lägger jobb i kön via ett gränssnitt i `Contracts` (`IAiJobs.EnqueueAsync`).
Varje modul har sin egen databaskoppling, så jobbet sparas i en egen transaktion och inte i samma som ändringen som
utlöste det, som design.md först sa. *Mitt beslut:* jobben är säkra att köra om, och om ett jobb inte sparas ser
kunden det och kan försöka igen. design.md uppdateras.

## Leverantörer

- Ett gränssnitt i AI-modulen för textsvar: frågan in, svaret och förbrukningen (tokens in och ut, modell, tid) ut.
  Fototolkningens bildtolkning och drömbildens rendering får egna gränssnitt i delprojekt 8 och 10.
- Claude-implementationen använder Anthropics officiella NuGet-paket `Anthropic` och modellen i inställningen
  `Anthropic:Model` (standard `claude-haiku-5-5`, Josefs val 2026-11-17: testjobbet behöver ingen större modell, och
  varje AI-stegs modell väljs genom mätning i delprojekt 8).
- Nyckeln läses från inställningen `Anthropic:ApiKey` i Worker. Lokalt kommer den från user secrets, i drift från
  Key Vault (delprojekt 14). Saknas den misslyckas jobbet direkt med "Ingen API-nyckel för Claude är inställd." och
  Worker loggar en varning när den startar.
- Svar som stoppas av leverantören (till exempel `refusal`) eller som avbryts (`max_tokens`) räknas som misslyckade
  försök, med orsak i loggen.

## Kostnadslogg

Tabellen `ai.calls`, en rad per anrop till en leverantör, också misslyckade:

- jobb, promptversion, leverantör, modell, start och tid i millisekunder, tokens in och ut, kostnad (Money i SEK), utfall
  (`ok`, `refused`, `error`) och felets typ.

Kostnaden räknas ut i koden: tokens gånger pris per miljon tokens i dollar, gånger en växelkurs. Priser per modell och
kursen ligger i inställningar (`Ai:Prices`, `Ai:UsdToSek`), så de kan ändras utan ny kod. *Mitt beslut:* priset för
`claude-opus-5-5` är 4 dollar per miljon tokens in och 20 ut, för `claude-haiku-5-5` 0,10 och 0,50, och kursen 10,50
kronor per dollar tills någon ändrar den.

**En modell utan pris** i `Ai:Prices` får aldrig anropas, eftersom kostnaden då skulle räknas som noll och dagsbudgeten
sluta gälla. Jobbet misslyckas direkt utan nytt försök, och Worker varnar när den startar om den inställda modellen
saknar pris. *Mitt beslut efter granskningen.*

Promptversionen är ett namn och nummer i koden, till exempel `connection-test@1`. Promptarna ligger i kodförrådet.

## Kvoter och dagsbudget

Inställningarna sparas i tabellen `ai.settings` (en rad) och ändras av administratörer i admin:

- Dagsbudget i kronor. Förslag: 500 kr.
- Kvot per trädgård och sort: fototolkning 3, designomgång 5, drömbild 5 (design.md).
- En av/på-knapp per AI-steg: fototolkning, designförslag och drömbild (design.md: "Varje AI-steg kan stängas av för
  sig"). Ett avstängt steg behandlas som en nådd budget: inga nya jobb, och jobb i kön misslyckas med orsaken att
  steget är avstängt. Testjobbet kan inte stängas av. *Mitt beslut efter granskningen:* knapparna tas med nu, eftersom
  de annars kräver en migrering och en ny runda i admin senare.

**Kvoten** kontrolleras av API:t när ett jobb läggs i kön: antalet jobb av samma sort för trädgården, utom jobb som
`failed`. Är kvoten nådd svarar API:t med ett tydligt fel och lägger inget jobb. Testjobbet har ingen kvot.

**Dagsbudgeten** gäller ett svenskt dygn (Europe/Stockholm). Dagens kostnad är summan i `ai.calls` sedan midnatt.

- API:t lägger inga nya jobb när budgeten är nådd, och svarar med att AI-stegen är pausade för dagen.
- Worker kontrollerar budgeten före varje anrop. Är den nådd misslyckas jobbet direkt med orsaken "AI-stegen är
  pausade för i dag." och kunden får reservvägen.
- När kostnaden passerar 80 procent loggas en varning (larm i delprojekt 14).
- Ett anrop som redan är på väg får göra klart. Budgeten kan därför överskridas med som mest ett anrop per Worker som
  kör samtidigt. *Mitt beslut efter granskningen:* ingen reservation av kostnad i förväg; med en eller ett par Workers
  är överskridandet några kronor.

## Testjobbet i admin

- En ruta "AI" i admin, synlig för administratörer:
  - knappen **Testa AI-anslutningen**;
  - dagens kostnad och dagsbudgeten, till exempel "12,40 kr av 500 kr i dag";
  - fält för dagsbudget och de tre kvoterna, och en av/på-knapp per AI-steg, med Spara.
- Knappen startar ett jobb av sorten `connection-test`. Worker frågar Claude något kort och fast ("Svara med en mening
  på svenska: vad är en trädgård?") och sparar svaret.
- Admin frågar efter jobbets status varannan sekund och visar sedan svaret, modellen, tiden och kostnaden, eller
  orsaken om det misslyckades.

## API

- `POST /api/ai/connection-test`: administratör. Lägger ett testjobb, svarar 202 med jobbets id.
- `GET /api/ai/jobs/{id}`: administratör för testjobb. I delprojekt 8 också kunden som äger jobbet; en annans jobb
  svarar 404, som designerna.
- `GET /api/ai/settings` och `PUT /api/ai/settings`: administratör. Inställningarna (budget, kvoter och av/på per steg)
  och dagens kostnad.
- Policynamnen för personal och administratör flyttas till `Contracts`, så att AI-modulen kan använda dem utan att
  referera Identity-modulen.

## Det som är kvar åt byggaren

- Exakt hur jobbtyper registreras, så att delprojekt 8 kan lägga till en ny sort utan att röra kön.
- Formen på `input` och `result` för testjobbet.
- Hur tester ersätter Claude med en låtsasleverantör, så att inga tester kostar pengar.
- Hur adminrutan ser ut, inom det som står ovan.

## Risker

- **Kostnad:** fel i omförsöken kan ge många anrop. Tre försök per jobb och dagsbudgeten sätter taket.
- **Växelkurs och priser** i inställningarna kan bli inaktuella. Kostnadsloggen är en uppskattning; Anthropics faktura
  är sanningen.
