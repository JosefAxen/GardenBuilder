# Designförslag – design

Delprojekt 9 i [färdplanen](../plans/2026-11-13-roadmap.md). Källan för regler är [docs/design.md](../../design.md):
"Kundens flöde" steg 3, "AI-flödena" (Så blir designförslagen giltiga) och anti-målen. Jobbet körs i kön från
[delprojekt 7](2026-11-17-ai-jobs-design.md). Nuläget kommer från [delprojekt 8](2026-11-18-photo-interpretation-design.md).

## Syfte

När kundens nuvarande trädgård finns i editorn vill hen se hur den *kunde* bli. Kunden anger stil, budget, skötselnivå
och växtzon och får två förslag som gör om **hela trädgården**: uteplats, gångar, grus, gräsmattans form, rabatter,
häckar och träd. Förslagen byggs bara av företagets artiklar i lager, klarar växtzonen och håller budgeten. Kunden väljer
ett och designar sedan vidare själv. Förslagen är säljverktygets "wow": de ska se genomtänkta ut och kunna offereras.

## Vägval

| Väg | Så funkar det | Avvägning |
| --- | --- | --- |
| **A. Byggklossar och layoutmotor (vald)** | AI:n väljer byggklossar (moduler) per zon med ungefärligt läge, storlek och arter. Kod räknar exakta former och lägen. | Nästan alltid giltiga och snygga förslag, rätt pris. Variationen begränsas av byggklossarna. |
| B. AI:n ritar själv | AI:n ritar alla ytor och växter i koordinater; koden kontrollerar. | Friast, men provet i delprojekt 8 visade meterfel: överlapp, många dyra omförsök. |
| C. Rutnät | AI:n målar tomten i 1×1 m-rutor; koden gör former av rutorna. | Friare än A, men kantiga former och mest nytt att bygga. |

*Josef valde A 2026-11-18. Josef valde också att förslagen gör om hela trädgården, inte bara planterar, och att
befintliga saker står kvar enligt regeln nedan.*

## Mål

- Kunden anger sina önskemål och får två förslag per omgång, var och en en hel scen med pris.
- Tomten delas av kod i namngivna zoner med yta, väderstreck och ljus.
- Kod väljer högst 150 artiklar i lager som klarar växtzonen.
- AI:n väljer byggklossar per zon; en layoutmotor i C# räknar fram exakta former och lägen.
- Vanlig kod kontrollerar varje förslag. Vid fel får AI:n felen tillbaka och högst två nya försök.
- Kunden provar ett förslag i 3D, ångrar eller väljer det. Ett valt förslag sparas som en designversion.

## Inte mål

- Dörrar, fönster och trappor. Scenen vet inte var de finns; uteplatsen läggs mot den fasad som passar bäst.
- Sluttningar, belysning, bevattning, murar och pooler.
- Förråd, pergola och andra byggnader (stöds inte av modellbiblioteket).
- Fler än två förslag per omgång. Fler omgångar ger fler förslag (kvoten är 5 omgångar per trädgård).
- Kundflödets egna sidor (delprojekt 12). Här byggs en panel "Förslag" i editorn, som "Foton" i delprojekt 8.

## Det som inte får hända (anti-mål)

- Ett förslag med en artikel som saknas, är slut i lager eller inte klarar kundens växtzon.
- Ett förslag som tar bort något kunden inte själv har markerat för borttagning (utom gräsmatta, se nedan).
- Ett förslag vars pris ligger över kundens budget utan att det syns.
- Att AI-svar hamnar i designen utan att vanlig kod har kontrollerat dem, eller utan att kunden har valt förslaget.
- Att kunden fastnar: misslyckas förslagen visas "Vi kunde inte ta fram förslag just nu. Designa vidare själv i katalogen."

## Vad som får ändras i trädgården

*Josefs beslut 2026-11-18.*

- **Gräsmattan är fri mark.** Förslaget får lägga uteplats, gångar, rabatter och träd där gräs finns i dag. Det
  befintliga gräset delas: det som blir kvar behåller status `existing-keep`, den bortskurna delen blir ett eget
  element med status `existing-remove` så att borttagningen prissätts. Ny gräsmatta (sådd eller rullgräs) läggs bara
  där det inte är gräs i dag.
- **Allt annat befintligt står kvar**: träd, buskar, häckar, staket, befintliga ytor. Förslaget bygger runt dem, med
  marginal (växter: halva fullvuxna bredden, ytor: 0,3 m).
- **Det kunden har tagit bort** (status `existing-remove`) räknas som fri mark.
- **Huset och tomtgränsen** rörs aldrig.

## Kundens önskemål

| Fält | Val | Används till |
| --- | --- | --- |
| Stil | Naturlig, Modern, Klassisk, Lantlig | Promptens beskrivning, artikelurvalet (färg, form) |
| Budget | under 50 000 kr, 50–150 000 kr, 150–300 000 kr, över 300 000 kr (inklusive arbete och moms) | Valideringen: prisspannets mitt ska ligga i ramen |
| Skötsel | Låg, Medel, Hög | Promptens regler: låg = inga perennrabatter över 10 m², mer grus och buskar |
| Växtzon | I–VIII, med en kort hjälptext | Artikelurvalet: artikelns zon ≥ kundens zon |
| Önskemål i fritext | Valfri, högst 300 tecken, till exempel "plats för barnens studsmatta" | Promptens beskrivning |

*Mina val: listorna ovan.* Värdena sparas med jobbet. Växtzonen hämtas senare från adressen (delprojekt 12).

## Hur det fungerar

### 1. Zoner (kod)

Fri mark = tomten minus huset, minus allt befintligt som står kvar (med marginal), plus gräs och det kunden har tagit
bort. Den delas i högst åtta zoner:

- **Vid huset:** en zon per fasad, 4 m djup ("söder om huset", "väster om huset" …).
- **Längs gränsen:** en zon per sida av tomten, 3 m djup ("längs norra gränsen" …), där den inte redan är vid huset.
- **Mitten:** resten.

Zoner under 4 m² slås ihop med grannen. Varje zon får namn, yta i m², form och **ljus**: *sol* söder och väster om
huset, *skugga* norr om huset inom husets höjd × 1,5, *halvskugga* i övrigt och intill befintliga träd. Zonerna är
desamma för båda förslagen.

### 2. Artikelurval (kod)

Högst 150 artiklar som är i lager och inte utgångna: alla ytor (plattor, trädäck, grus, gräs, rabattjord), staket och
möbler, och växter vars zon ≥ kundens zon. Växter sorteras efter hur många zoners ljus de passar och klipps till 150.
AI:n får id, namn, typ, fullvuxen höjd och bredd, ljus och pris per enhet.

### 3. Byggklossar (AI:n väljer, kod lägger ut)

| Byggkloss | Parametrar | Layoutmotorn |
| --- | --- | --- |
| Uteplats | zon (vid huset), yta m², artikel (plattor/trädäck) | Rektangel mot fasaden, så bred som fasaden tillåter, djup = yta / bredd |
| Gång | från (uteplats eller zon), till (zon), artikel (grus/plattor), bredd 0,8–1,2 m | Kortaste väg runt hinder, som yta |
| Grusyta | zon, yta m², artikel | Rektangel eller zonens form, krympt till ytan |
| Perennrabatt | zon, längd m, djup m, 2–5 arter | Rabattjord som yta längs zonens längsta kant; växter i grupper om 3–5, avstånd efter fullvuxen bredd |
| Buskgrupp | zon, 1–3 arter, antal | Klunga med avstånd efter fullvuxen bredd |
| Häck | gränssida, art, längd m | Linje längs gränsen, 0,5 m innanför |
| Solitärträd | zon, art | Där zonen är bredast, minst halva kronbredden från allt |
| Fruktträd | zon, art, antal | I rad med 4 m mellanrum eller efter kronbredd |
| Gräsmatta | zon eller "resten" | Ny gräsyta där det inte är gräs i dag; befintligt gräs som inte används står kvar |
| Möblering | uteplatsen, 1–2 artiklar | Mitt på uteplatsen |

AI:n svarar med strukturerad utdata: ett namn och en kort motivering på svenska per förslag, och en lista byggklossar.
Prompten versioneras: `design-proposal@1`.

### 4. Kontroll och omförsök (kod)

Layoutmotorn bygger scenen. Sedan kontrolleras den:

- `ISceneCheck` (samma regler som vid sparning).
- Varje artikel finns, är i lager och är inte utgången. Varje växt klarar växtzonen.
- Växtens ljus passar zonens ljus.
- Inga befintliga element som ska stå kvar är borttagna eller överlappade.
- Priset (Pricing-modulens beräkning) har sin mitt inom budgetramen.
- Byggklossar som layoutmotorn inte kunde lägga ut (för liten zon) räknas som fel.

Felen skickas tillbaka till AI:n på svenska ("Uteplatsen på 40 m² får inte plats söder om huset, högst 22 m²") och den
får försöka igen, högst två gånger. Klarar förslaget sig ändå inte tas byggklossar med fel bort och förslaget visas om
minst hälften är kvar; annars utgår det. Om båda förslagen utgår får kunden reservvägen.

### 5. Två förslag

Ett jobb `design-proposal` gör två förslag efter varandra: **"Nära din stil"** (följer önskemålen troget) och
**"Lite djärvare"** (samma ramar, mer variation). Varje förslag är ett eget anrop med egna omförsök. Modell:
`Anthropic:Models:design-proposal`, *mitt val: Sonnet 5.5*, samma skäl som i delprojekt 8. Kvoten (5 omgångar per
trädgård) och dagsbudgeten gäller.

Resultatet sparas på jobbet: för varje förslag namn, motivering, hela scenen och prisspannet.

### 6. I editorn

- Panelen **"Förslag"** (knapp i verktygsraden, som "Foton"): formulär med önskemålen och knappen "Ge mig förslag".
  Under tiden "Tar fram förslag …"; polling varannan sekund, ger upp efter 8 minuter.
- Två kort med namn, motivering och prisspann. **"Prova"** ersätter scenen med förslaget som ett kommando, så
  "Ångra" tar tillbaka nuläget. **"Välj det här"** behåller det och sparar en designversion med anledningen AI-förslag.
- Senaste omgången hämtas igen när panelen öppnas (som i delprojekt 8).
- Reservvägen: "Vi kunde inte ta fram förslag just nu. Designa vidare själv i katalogen."

## Kontrakt mellan moduler

- **Catalog → Ai:** ett nytt kontrakt som listar artiklar i lager med växtfält (zon, ljus, fullvuxen höjd och bredd,
  enhet, pris, form, familj).
- **Pricing → Ai:** ett nytt kontrakt som räknar prisspannet för en scen, med samma kod som editorns pris.
- **Design → Ai:** `IDesignWorkingCopies` (finns) och `IDesignVersions` (finns, anledningen `AiProposal` finns).

## Test

- Enhetstester för zonerna (väderstreck, ljus, sammanslagning), artikelurvalet (zon, lager, 150) och varje byggkloss i
  layoutmotorn (får plats, krockar inte, avstånd), och för kontrollen (budget, ljus, zon, befintligt orört).
- Gräsdelningen: bortskuret gräs blir `existing-remove` med rätt yta, resten står kvar.
- Jobbet med en låtsasmodell: giltigt förslag, ogiltigt först och giltigt efter omförsök, ogiltigt tre gånger, ett av
  två förslag utgår, båda utgår.
- Adresser: ägarkontroll, kvot, och att designen inte ändras av jobbet.
- Editorn: prova och ångra, välj sparar version, återhämtning efter omladdning.
- Ett riktigt anrop mot Claude på Josefs testträdgård innan delprojektet är klart, efter att han sagt ja.

## Svar på granskarens frågor

- **Växtzonens riktning.** Katalogens växtzon (heltal 1–8, krävs för växter) följer plantskolornas regel: "zon V" betyder
  att växten klarar zon I till V. Högre siffra är kallare. En växt passar alltså när *artikelns zon ≥ kundens zon*.
  Importmallens hjälptext och katalogdesignen får en rad om det. Företagets riktiga lista måste följa samma regel.
- **Budgeten** är ett tak. Prisspannets mitt ska ligga under ramens övre gräns (ramen "över 300 000 kr" har inget tak).
  Billigare än ramen är tillåtet. Ligger förslaget över taket blir det ett fel som skickas tillbaka till AI:n. Är det
  fortfarande över efter omförsöken visas förslaget ändå, med "Över din budget" på kortet. *Mitt val: ett dyrt men
  snyggt förslag är bättre än inget, så länge det syns.*
- **Befintligt gräs** är ytor med `material` gräs, med eller utan artikel. Gräsdelningen görs med Clipper2, som redan
  finns i Ai-modulen. Bitar under 0,5 m² (både kvar och bortskurna) slängs i stället för att bli egna element.
- **Förslaget är en hel scen.** Tomten, huset och allt befintligt kopieras oförändrat med samma id. Det bortskurna gräset
  får nya id, och det kvarvarande gräset behåller det gamla id:t med ny form. Därför fungerar "Ångra" och prissättningen.
- **Fasader.** Varje kant på huset som är minst 2 m lång är en fasad. Den får namn efter väderstrecket den vetter mot,
  avrundat till närmaste av fyra. Kanter med samma väderstreck bildar tillsammans en zon. AI:n väljer själv
  uteplatsens zon bland zonerna vid huset; prompten säger att sol är att föredra.
- **Byggklossar som faller bort.** Faller en uteplats bort, faller också gången och möbleringen som hör till den.
  "Hälften kvar" räknar alla byggklossar, även de som föll med. Kontrollen, budgeten inräknad, körs om efter bortfallet.
- **Kostnad och kvot.** Ett jobb gör högst sex anrop (två förslag med två omförsök var). Dagsbudgeten kontrolleras före
  varje anrop, som i dag. Stoppar den mitt i jobbet visas det förslag som hann bli klart, annars reservvägen. Ett
  misslyckat jobb räknas inte mot kvoten (så fungerar kön redan).
- **Önskemålen** sparas i jobbets indata (JSON), som fotona i delprojekt 8. Ingen ny tabell.

## Kvar för den som bygger

- Exakta algoritmer i layoutmotorn så länge reglerna i tabellen och kontrollen håller.
- Promptens formulering och JSON-schemat, så länge versionen höjs vid ändring.
- Hur korten ser ut i panelen.
