# Delprojekt 3: Identitet

Status: förslag, väntar på Josefs godkännande.

## Varför

Delprojekt 4 (Trädgård och design) ska spara kundens trädgård och designer, och varje anrop måste kunna avgöra vems
de är. Admin ska bara kunna öppnas av personal. Identitet ger båda: ett sätt att känna igen kunden och en inloggning för
personalen.

Vi bygger i färdplanens ordning. E-posttjänsten är inte vald än, så kunden loggar inte in med engångskod i det här
delprojektet. Kunden är i stället anonym tills e-posten finns.

## Mål

- En kund känns igen på en säker kaka, utan inloggning, utan namn och utan e-post.
- Delprojekt 4 kan i varje anrop fråga vem den aktuella kunden är, och skapa kunden när något sparas första gången.
- Personal (Säljare och Administratör) loggar in med lösenord och tvåfaktor. Tvåfaktor är obligatorisk.
- Personalkonton skapas och återställs med ett kommando. Ingen kan registrera sig själv.
- Admin-sidan i webben kräver inloggad personal.

## Inte med nu

- Engångskod via e-post, bekräftad e-post och "fortsätt via länk i e-posten". De kommer när e-posttjänsten är vald.
  Bekräftad e-post blir då en spärr före första AI-jobbet, som design.md säger.
- En sida i admin för att hantera personal. Den kommer i delprojekt 11.
- Begränsning av anrop per IP-adress, säkerhetshuvuden och fullt skydd mot förfalskade anrop. De kommer i delprojekt 13.
- Samtycken. De hör till integritetstexten i delprojekt 12 och 13.
- Inloggning via Google, Microsoft eller liknande.

## Får inte hända

- En kund kommer åt en annan kunds data, eller en kund kommer åt admin.
- Personal kommer in utan tvåfaktor.
- Inloggningsuppgifter eller sessioner sparas i webbläsarens lagring (localStorage eller liknande), eller kan läsas av
  JavaScript.
- Lösenord, koder eller e-postadresser hamnar i loggar.
- Kunder tappar sina trädgårdar när servern startas om eller driftsätts på nytt.
- Varje sidvisning eller sökmotor-robot skapar en ny kund i databasen.

## Vägval

| Väg | För | Emot |
| --- | --- | --- |
| **ASP.NET Core Identity (vald)** | Färdig och beprövad: hashade lösenord, låsning efter fel, tvåfaktor med app (TOTP), reservkoder och roller. Passar modulstrukturen. | Många delar vi inte använder. |
| Allt skrivet själva | Bara det vi behöver, inget dolt. | Egen säkerhetskod där fel är farligast. |
| Extern tjänst (Entra, Auth0) | Någon annan sköter personalens inloggning. | Ett konto, en kostnad och inställningar till, för en handfull anställda. |

Josef valde ASP.NET Core Identity. Han valde också anonyma kunder (före en engångskod som skrivs i loggen) och att
personalkonton skapas med ett kommando (före en personalsida nu).

## Kunden

- Kunden är en rad i en egen tabell i schemat `identity`: id (UUID v7), när kunden skapades och när kunden senast sågs.
  Kolumner för e-post och bekräftad e-post läggs till när e-posten byggs, inte nu. Kunden använder inte tabellerna i
  ASP.NET Core Identity.
- Kunden skapas först när något sparas, inte vid sidvisning. Kontraktet till andra moduler (i `src/Contracts`) har två
  delar:
  - läsa den aktuella kundens id, eller inget om webbläsaren saknar en giltig kaka
  - hämta eller skapa den aktuella kunden, vilket sätter kakan om den saknas
- Kakan:
  - är `HttpOnly`, så att JavaScript inte kan läsa den
  - skickas bara över https (`Secure`)
  - har `SameSite=Lax`
  - gäller i 90 dagar och förnyas vid varje besök
  - innehåller bara kundens id, krypterat av ASP.NET Core
- En kaka för en kund som inte längre finns behandlas som ingen kaka.
- "Senast sedd" uppdateras högst en gång per dygn per kund, så att varje anrop inte skriver i databasen. Den används
  senare för lagringstiden (90 dagars inaktivitet).
- En kund som tappar kakan, till exempel genom att rensa webbläsaren eller byta dator, kommer inte åt sin trädgård igen
  förrän e-posten finns. Det accepterar vi tills vidare.

## Personalen

- ASP.NET Core Identity med id av typen UUID v7. Tabellerna ligger i schemat `identity` med snake_case-namn, som allt
  annat.
- Rollerna heter Säljare och Administratör. En person har en roll.
- Lösenord: minst 12 tecken, utan krav på siffror eller specialtecken (NIST:s rekommendation). Kontot låses i 15 minuter
  efter 5 felaktiga försök.
- Tvåfaktor med en app i mobilen (TOTP), och 10 reservkoder som visas en gång. Personal kan inte använda admin utan
  aktiverad tvåfaktor.
- Inloggningen sparas i en egen kaka för personal:
  - `HttpOnly`, `Secure` och `SameSite=Strict`
  - gäller i 8 timmar och förnyas vid aktivitet, men aldrig längre än 12 timmar från inloggningen
- Första inloggningen:
  1. E-post och tillfälligt lösenord.
  2. Byt lösenord.
  3. Koppla tvåfaktor-appen: QR-kod och nyckeln i text, sedan en kod från appen för att bekräfta.
  4. Reservkoderna visas.
  5. Inne.
- Senare inloggningar: e-post och lösenord, sedan en kod från appen eller en reservkod.
- Utloggning tar bort kakan i den webbläsaren.
- **Hur snabbt en session dör** *(byggarens beslut)*: servern kontrollerar varje inloggad persons säkerhetsstämpel mot
  databasen en gång i minuten. Att spärra, återställa eller byta lösenord ändrar stämpeln. Då avslutas alla den
  personens sessioner, i alla webbläsare, inom en minut. Det kostar en liten databasfråga per person och minut.

## Kommandot för personal

Ett verktyg i `tools/`, likt katalogimporten. Det körs från Aspire-dashboarden eller terminalen lokalt, och som ett
engångsjobb i drift. Det kan:

- **Skapa** en person med e-post, namn och roll. Verktyget slumpar ett tillfälligt lösenord som måste bytas vid första
  inloggningen och som slutar gälla efter 24 timmar.
- **Återställa** en person. Verktyget ger ett nytt tillfälligt lösenord på samma sätt, tar bort tvåfaktor och
  reservkoder, låser upp kontot och avslutar personens sessioner. Personen gör om första inloggningen.
- **Spärra** och **öppna** en person.
- **Lista** personalen med roll och status, utan lösenord eller koder.

Varje åtgärd loggas med personens id och vad som gjordes (design.md: administrativa åtgärder loggas), men aldrig med
lösenordet.

**Var det tillfälliga lösenordet hamnar** *(byggarens beslut)*: Aspire-dashboarden sparar och visar allt ett verktyg
skriver ut. Lösenordet skrivs därför aldrig till konsolen. Lokalt hamnar det i en fil i en mapp som git ignorerar, och
konsolen säger bara var filen ligger. Den som skapade kontot lämnar lösenordet till personen och raderar filen. I drift
(delprojekt 14) lämnas lösenordet i stället via Key Vault. E-postadressen syns som argument i den lokala dashboarden,
vilket är godtagbart lokalt. "Lista" visar namn, roll och status, inte e-post.

## Webben

- `/admin` och allt under det kräver inloggad personal. Annars visas inloggningssidan.
- Skärmarna på svenska:
  - logga in
  - byt lösenord
  - koppla tvåfaktor
  - ange kod
  - reservkoder
  - en knapp för att logga ut
- Webben frågar servern vem som är inloggad. Den sparar inget om inloggningen själv.
- Kundens sidor visar inget nytt i det här delprojektet. Kakan sätts av servern när delprojekt 4 sparar något.

## API:et

- Alla adresser ligger under `/api/identity`. Det behövs adresser för:
  - vem som är inloggad, som svarar personal (med roll), kund eller ingen
  - personalens inloggning i steg: lösenord, byt lösenord, koppla tvåfaktor, kod och utloggning
- Kundens kaka och personalens kaka är två skilda inloggningssätt (authentication schemes). En personalkaka gör ingen
  till kund, och tvärtom.
- Två behörighetsregler (policies): **Personal** (Säljare eller Administratör, med tvåfaktor klar) och **Administratör**.
  Befintliga adresser (katalogen och priset) är fortsatt öppna.
- Fel ges som en kod och ett svenskt meddelande i klienten, som i prissättningen. En fel e-post och ett fel lösenord ger
  samma svar, så att man inte kan pröva vilka e-postadresser som finns.

## Nycklarna som krypterar kakorna

ASP.NET Core krypterar kakorna med nycklar (Data Protection). Om nycklarna bara ligger i serverns minne eller på en disk
som försvinner, blir alla kakor ogiltiga vid omstart. Då tappar varje anonym kund sin trädgård. Nycklarna sparas därför
i databasen, i schemat `identity`, så att de överlever omstarter och fungerar med flera serverinstanser.

**Nycklarna krypteras först i drift** *(byggarens beslut)*: den som kan läsa nyckeltabellen kan förfalska en
personalkaka. Lokalt är det bara utvecklaren som når databasen, så nycklarna sparas okrypterade nu. Innan appen tar
emot riktiga kunder (delprojekt 14) krypteras de med en nyckel i Azure Key Vault. Det skrivs in i färdplanen som ett
krav före lansering.

## Tester

Åtkomstkontroll är ett av de områden där testerna läggs tyngst (design.md). Minst dessa:

- **Kunden:**
  - Hämta-eller-skapa ger samma kund vid nästa anrop med kakan, och en ny kund utan kaka.
  - En förfalskad eller ändrad kaka ger ingen kund.
  - En kaka för en raderad kund ger ingen kund.
  - En sidvisning utan sparande skapar ingen kund.
- **Personalen:**
  - Rätt lösenord men ingen tvåfaktor ger ingen åtkomst till Personal-adresser.
  - Fel kod ger inget.
  - En reservkod fungerar en gång.
  - Kontot låses efter fem fel.
  - Ett tillfälligt lösenord måste bytas.
  - En säljare når inte Administratör-adresser.
- **Kakorna:** kundkakan når inte personaladresser, och personalkakan gör ingen till kund.
- **Nycklarna:** en kaka är fortfarande giltig efter att API:et startats om, med samma databas.
- **Sessioner:** en spärrad eller återställd person nekas inom en minut, även med en öppen session.
- **Tillfälligt lösenord:** ett som är äldre än 24 timmar fungerar inte.
- **Loggarna:** loggar från inloggningen innehåller inga lösenord, koder eller e-postadresser.
- **Kommandot:** skapa, återställa och spärra fungerar mot en riktig databas.

## Design.md uppdateras i samma ändring

- "Kunder loggar in utan lösenord, med engångskod via e-post" blir: kunder är anonyma och känns igen på en säker kaka.
  Engångskod via e-post kommer när e-posttjänsten är vald.
- Steg 1 i kundflödet, "bekräftar sin e-post med en engångskod", markeras som senare.
- Färdplanens rad för delprojekt 3 ändras på samma sätt.
- Färdplanen får under delprojekt 14 kravet att Data Protection-nycklarna krypteras med Key Vault, och att det
  tillfälliga lösenordet lämnas via Key Vault, före lansering.

## Lämnat till byggaren

- Exakta namn på tabeller, adresser, kakor och klasser.
- Hur verktyget för personal tar emot sina argument från Aspire-dashboarden.
- Vilket bibliotek som ritar QR-koden i webben.
- Utseendet på inloggningsskärmarna, inom tonen i resten av admin.
