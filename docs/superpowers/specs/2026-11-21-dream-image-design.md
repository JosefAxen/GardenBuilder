# Drömbild – design

Delprojekt 10 i [färdplanen](../plans/2026-11-13-roadmap.md). Källan för regler är [docs/design.md](../../design.md):
"Kundens flöde" steg 5, "AI-flödena" (Drömbild) och anti-målen. Jobbet körs i kön från
[delprojekt 7](2026-11-17-ai-jobs-design.md). Fotona och var de togs kommer från
[delprojekt 8](2026-11-18-photo-interpretation-design.md). Utseendet följer [DESIGN.md](../../../DESIGN.md).

## Syfte

Den stiliserade 3D-scenen visar *var* allt står, men inte hur det kommer att kännas. Drömbilden ger realismen: kunden
väljer en vy och får en fotorealistisk bild av just sin design, i sin egen trädgård. Den ska få kunden att vilja skicka
offertförfrågan, utan att lova något annat än det som offereras.

Josef vill kunna testa kundens hela resa (foton → nuläge → förslag → egen design → drömbild → offertförfrågan) innan
bildtjänsten är vald. Nycklar till bildtjänster skaffas senare. Delprojekt 11 och 12 byggs direkt efter detta.

## Mål

- Kunden sparar vyer i 3D-editorn ("Från altanen") och skapar en drömbild från en sparad vy.
- Drömbilden styrs av 3D-renderingen från vyn, så att växter, ytor och huset hamnar där de står i designen.
- **Kundens egna foton skickas med som referensbilder** (Josefs beslut 2026-11-21), så att bilden blir så lik den
  verkliga trädgården som möjligt: huset, grannskapet, ljuset och det som står kvar.
- Bilden visas märkt "Inspirationsbild" i ett galleri i editorn och kan öppnas stort och laddas ner.
- Högst 5 drömbilder per trädgård (kvoten finns), dagsbudgeten och av/på-knappen gäller som för de andra AI-stegen.
- Ett **testläge** gör att hela flödet fungerar utan nycklar. Riktiga bildtjänster kopplas in med en inställning.

## Icke-mål

- Att välja bildtjänst. Det görs senare med ett prov på Google (Gemini) och Black Forest Labs (FLUX) sida vid sida.
  Gränssnittet ska rymma båda; implementationerna skrivs när nycklarna finns.
- Redigering av drömbilden (be om ändringar, måla bort saker).
- Drömbilder i admin och i offerten (kan läggas till i 11).
- Drömbild på mobil. Editorn är för dator.

## Anti-mål

- En drömbild som visar något annat än designen: fler växter, andra arter, en pool som inte finns. Bilden ska alltid
  märkas "Inspirationsbild" och texten ska säga att offerten gäller designen, inte bilden.
- Personuppgifter som hamnar hos en leverantör utan att kunden vet det. Panelen säger rakt ut att 3D-bilden och
  kundens foton skickas till bildtjänsten.
- AI-kostnader utan tak.
- En kund som fastnar: går det inte att skapa en bild visas 3D-vyn som den är, med en förklaring.

## Vägval

| Väg | Så funkar det | Avvägning |
| --- | --- | --- |
| **A. Editorn renderar vyn och laddar upp den (vald)** | Webbläsaren ritar 3D-scenen från den sparade vyn till en PNG och skickar den med jobbet. Workern skickar renderingen, utvalda foton och en växtlista till bildtjänsten. | Exakt samma bild som kunden ser. Ingen 3D-motor på servern. |
| B. Servern renderar | Workern ritar scenen med en huvudlös 3D-motor. | Behöver en egen rendering på servern som kan glida från editorns. Mycket mer att bygga. |
| C. Bara text | Bildtjänsten får en beskrivning av designen. | Växterna hamnar inte där de står. Bryter mot design.md. |

*A är det enda rimliga sättet; Josef godkände upplägget 2026-11-21 med ändringen att kundens foton skickas med.*

## Hur det fungerar

### Vyer i scenen (scenformat 3)

- `SceneDocument` får en lista `Views`: `{ Id, Name, Eye: {X, Y, Height}, Target: {X, Y, Height}, FieldOfView }`.
  Meter och grader. `Name` är kundens namn på vyn, högst 40 tecken. Högst 8 vyer per design.
- `CurrentFormatVersion` blir 3. Version 1 och 2 läses och uppgraderas med en tom lista. Validering: ögat och målet
  inom tomten med 10 m tolerans (man får stå på gatan), höjd 0,5–30 m, synfält 20–90 grader, namn inte tomt.
- Editorn får kommandona `add-view` och `remove-view`, som går att ångra. Typer och schema genereras som förut
  (`npm --prefix src/web run gen:scene`).

### I editorn: panelen "Drömbild"

- En fjärde knapp i hyllans panelväljare: Katalog / Foton / Förslag / **Drömbild**. Panelen öppnas bredvid
  verktygsskenan som de andra.
- **Vyer:** "Spara den här vyn" sparar 3D-kamerans läge med ett namn som kunden skriver. Varje sparad vy visas med
  namn, "Visa" (flyttar kameran dit) och "Ta bort". Utan vyer visas en kort förklaring.
- **Skapa drömbild:** varje vy har "Skapa drömbild". Editorn ritar scenen från vyn i 1536 × 1024 och skickar PNG:n med
  jobbet. Under tiden visas förloppet och "Det tar ungefär en minut".
- **Integritet:** en förkryssad ruta "Skicka med mina foton (blir mer likt din trädgård)" ovanför knappen, och texten
  att 3D-bilden och, om rutan är ikryssad, dina foton skickas till bildtjänsten för att skapa bilden. Kryssar kunden
  ur rutan skickas bara 3D-bilden och växtlistan (Josefs beslut 2026-11-21). Valet sparas per trädgård i webbläsaren
  och skickas med varje jobb som `sendPhotos`.
- **Galleri:** färdiga bilder, nyast först, med vyns namn och datum. Varje bild bär etiketten "Inspirationsbild"
  och texten "Offerten gäller designen, inte bilden." Klick öppnar bilden stort med "Ladda ner" och "Stäng".
- **Kvot:** "3 av 5 drömbilder kvar". Vid 0 är knappen avstängd med förklaring.
- **Fel och reservväg:** misslyckas jobbet visas 3D-renderingen i galleriet med "Det gick inte att skapa en drömbild
  just nu. Här är 3D-vyn." Testläget visar sin stämpel tydligt.
- Allt i Plantskolans komponenter och tokens.

### På servern

- **Endpoints** i Ai-modulen, som för förslagen: `POST /api/gardens/{g}/dream-images` (multipart: designId, viewId,
  PNG), `GET /api/gardens/{g}/dream-images` (galleriet), `GET /api/gardens/{g}/dream-images/{id}`. Ägarskap som i
  delprojekt 9: allt annat svarar som om det inte fanns.
- **Uppladdning:** PNG högst 8 MB och 4096 px, avkodas och kodas om med Magick.NET (som fotona) till JPEG, sparas
  privat i Blob Storage i en egen container `dream-images`. Endast Ai-modulen läser den.
- **Jobbet `dream-image`:** handlaren läser designen (vyn måste finnas), renderingen, artiklarna i scenen och, när
  jobbet har `sendPhotos`, upp till **två foton**: de vars kameraläge ligger närmast vyn, viktat på riktning (skillnad i kurs) och avstånd. Foton utan
  kameraläge används bara om inga har läge. Inga foton alls är tillåtet.
- **Prompt** `dream-image@1` på engelska till bildtjänsten: gör den första bilden fotorealistisk; behåll varje
  föremåls plats, storlek och antal; lägg inte till växter, möbler eller byggnader; använd fotona för huset, omgivningen,
  ljuset och material; växterna är [artikelnamn med latinskt namn om det finns]; svensk trädgård, dagsljus.
- **Bildgränssnitt** `IImageModel` bredvid `ITextModel`: `Provider`, `ModelFor(kind)`, `EditAsync(ImageRequest)` där
  `ImageRequest(Prompt, Reference: byte[], Photos: byte[][], Width, Height)` och svaret är JPEG-bytes plus använd modell.
  `AiCalls` får `ImageAsync` med samma vakter som text: steget påslaget, pris finns för modellen, dagsbudget, lease
  förnyas, rad i `ai.calls`. Pris per bild läggs i `Ai:Prices` som ett fast belopp per bild.
- **Testläge:** `TestImageModel` (`Images:Provider = "test"`, förvalt i Development och tills nycklar finns) returnerar
  renderingen med stämpeln "Testbild" och en tunn kant, efter 3 sekunder, med kostnaden 0 kr. Den kan också
  tvingas att misslyckas (`Images:TestFailEvery`) så att felvägen går att prova.
- **Kontroll av svaret:** bilden måste gå att avkoda, ha rätt ungefärliga proportioner och vara större än 256 px.
  Bildtjänstens egen innehållsgranskning gäller; ett nej från den blir "kunde inte skapas".
- **Resultat** sparas i `dream-images` och jobbet pekar på bloben. Länkar till bilderna är SAS-länkar på 10 minuter,
  som fotona (rad 14 i färdplanen gäller även här).

### Radering

- Drömbilder hör till trädgården. När trädgården raderas (delprojekt 13) raderas bilderna också. Kontraktet för
  radering läggs till där; här räcker att bilderna ligger under trädgårdens id i containern.

## Verifiering

- Enhetstester: val av foton efter kameraläge, promptens innehåll, kontroll av svaret, scenformat 3 (läsa 1, 2, 3;
  validering av vyer), testmodellens stämpel och felläge.
- Integrationstester: uppladdning och jobb hela vägen med testmodellen (Azurite och PostgreSQL som i dag), ägarskap,
  kvoten 5, dagsbudget, avstängt steg.
- Webbtester: vy-kommandon och ångra, panelens tillstånd (inga vyer, köar, klar, fel, kvot slut).
- I webbläsaren: hela flödet i testläge med ett riktigt foto och en sparad vy.

## Beslut efter granskningen

En byggare läste dokumentet och ställde frågor. Besluten nedan är mina (Claude) och kan rättas av Josef.

- **Kvoten:** jobb som köar, körs eller är klara räknas mot de 5. Ett jobb som misslyckas lämnar tillbaka sin plats.
  Därför räknar kvoten in pågående jobb, och dubbelklick ger inte fler bilder än 5.
- **Val av foton:**
  - Bara foton med kameraläge används, och upp till två av dem.
  - Ett foto vars riktning skiljer mer än 120° från vyns tas inte med.
  - Bland resten vinner minst skillnad i riktning; avståndet avgör vid lika riktning.
  - Har inget foto kameraläge skickas de två först uppladdade.
  - Finns inga foton skickas bara 3D-bilden.
- **Vyns läge:** "Spara den här vyn" tar 3D-kamerans position som öga och kamerans målpunkt som mål, båda med höjd,
  och kamerans synfält. Ögat får ligga upp till 10 m utanför tomten, på höjd 0,5–30 m. Ingen ny kameratyp byggs.
- **Växtlistan:**
  - Den tar med alla artiklar i scenen med status ny eller behålls, som "namn × antal".
  - Möbler och ytor räknas med.
  - Latinska namn finns inte i katalogen och utgår.
  - Listan filtreras inte efter vad som syns från vyn.
- **Bildstorlek:**
  - Editorn ritar alltid 1536 × 1024.
  - Varje bildtjänst anpassar storleken inuti sin implementation, genom att skala eller fylla ut.
  - Kontrollen av svaret godtar proportioner mellan 4:3 och 16:9.
- **Länkar:** galleriet hämtar nya länkar på 10 minuter varje gång panelen öppnas eller en bild öppnas stort.
- **Misslyckade jobb:** den uppladdade 3D-bilden sparas alltid. Ett misslyckat jobb visas i galleriet med den och
  förklaringen. Det räknas inte mot kvoten och kan tas bort ur galleriet.
- **Designen ändras under tiden:**
  - Jobbet använder den uppladdade 3D-bilden, som är det kunden såg.
  - Varje bild sparar designens revision.
  - Galleriet visar "Designen har ändrats sedan bilden skapades" när revisionen inte längre är den aktuella.
  - En vy som tas bort påverkar inte jobb som redan köar.

## Kvar åt byggaren

- Exakt viktning när fotona väljs.
- Hur renderingen ritas i webbläsaren (en extra rendering i scenens Canvas från vyns kamera är troligast).
- Galleriets layout inom Plantskolan.
- Namn på inställningar och tabeller, så länge de följer projektets mönster.
