# Realms

## Overview

Feudal titles over the fiefs and realms of the campaign: who holds a land by right, how that right is inherited, and what happens when it is taken by force. Titles are generated on a campaign's first hourly tick, follow fief and ruler changes, and drive claims, grants, offices, weekly consequences and the scripted coronation of Aragorn. They are shown in TAOM's themed encyclopedia: a page for every title, a Titles category, title sections on settlement, hero and kingdom pages, and a title tree.

Realms shipped as the separate TAOM.Realms module (v0.1.0) until 2026-11-10, when its code moved into TAOM. The module's README and `docs/DESIGN.md` in `R:\TAOM\TAOM.Realms` hold the full rules list and design history.

## Why This Exists

- **Vanilla behavior:** a fief belongs to whoever owns it. Ownership is all there is: a siege transfers the land and nothing remembers whose it was. Every fief goes to the clan leader.
- **TAOM requirement:** Middle-earth's realms rest on right and lineage: an empty throne of Gondor with a Steward, a King's claim of blood, Dol Amroth's Prince, the Marshals of the Mark. Holders are styled by their title in their own culture's words.
- **Without this feature:** no vacant throne, no Steward, no Return of the King, and lords named without the lands they hold.

It is deliberately light (no claim wars, no de jure drift): Bannerlord is not a hardcore strategy game.

## Architecture

### Design Challenge

The rules are large (succession laws, claims, unions, offices, chronicle, grants to children) and must be testable without the game. The campaign layer reaches many engine actions (influence, gold, relations, fief gifts, the ruling clan, clan banners) and the UI is client-only (ADR-013). The save data outgrows the engine's 32 KB string limit.

### Solution Approach

- **Domain** (`Main/Features/Realms/Domain`, the former TAOM.Realms.Core): every rule, with no game references, fully unit tested. `RealmsService` is the facade.
- **Campaign coordinator** (`RealmsCampaign`, four partial files): the engine-facing layer. It reads the world into snapshots, calls the service, applies the resulting events through TaleWorlds actions and reports them. It is owned by the thin entry point `RealmsCampaignBehavior` (ADR-002), which registers its listeners and its save data. The class name `RealmsCampaignBehavior` is the save key and matches the old module, so old saves keep their titles.
- **Hooks** (`Hooks/`): `PartyNames` (lord party names, patched at GameInit) and `LiegeArmyDiscount` (a liege calls vassals for half the influence, patched at session launch). The client's `ConversationNames` styles the speaker's name.
- **Client** (`Client/Features/Realms`): the encyclopedia mixins and prefab extensions (settlement, hero and kingdom pages), the Titles encyclopedia category and its title pages (`RealmsTitle`, a client-side `MBObjectBase` per title, unregistered), the title tree, and the claimants and coronation popups. `RealmsClient` is called from `ClientSubModule` (game start and tick). Core shows the coronation through `IRealmsPopupAdapter`; core registers a null adapter and TAOM.Client replaces it. The banner crosses the seam as its serialized code (ServiceSignature gate).
- **Save data**: chunked and versioned (`taom_realms_chunks`, `taom_realms_chunk_<i>`, header `TAOM.Realms`). Data from a newer version is kept unchanged and the ledger turns off.
- **Co-op**: host only (see [coop-interop.md](coop-interop.md)). Every listener and every player action checks `ICoopSessionProvider.IsAuthority`; a client keeps the ledger it loaded with the host's save, read only, and never creates one.

### Component Diagram

```
realms_titles.xml
        |
  RealmsConfigLoader ---> RealmsConfig
        |
  RealmsService (Domain: catalog, ledger, succession, claims, feudal, offices)
        |
  RealmsCampaign (coordinator: snapshots in, TaleWorlds actions out)
     |            |                  |
RealmsCampaign  Hooks              IRealmsPopupAdapter
Behavior        (PartyNames,         |
(events, save)  LiegeArmyDiscount)  RealmsPopupAdapter (TAOM.Client)
                                     |
                         encyclopedia mixins, title pages, title tree, popups
```

## Configuration

### Config File: `Main/_Module/ModuleData/realms_titles.xml`

The file's header comment documents every element and attribute. Main sections:

| Section | Controls |
|---|---|
| `<Ranks>`, `<Prefixes>`, `<Styles>` | Title and holder names per rank, culture, gender and place (`known_over` keeps a lower title's style) |
| `<Duchy>` regions, `<HighTitle>`, `<Unions>` | The generated hierarchy above counties and baronies |
| kingdom `capital`, `throne="vacant"`, laws | Realm seats, Gondor's empty throne, succession and gender laws |
| `<Claims>`, `<Grants>` | Claim, usurp, form, restore, grant and revoke prices and relation changes |
| `<Offices>` | Steward, Marshals, Lieutenant: styles and weekly influence |
| `<Consequences>` | The weekly resentment and liege pull, the army discount, and `tribute_rank` (the lowest title that owes tribute, Barony) |
| `<Coronations>` | Aragorn's coronation: house, for a companion of your clan or for you playing Aragorn (your own clan is renamed House of Telcontar and takes the banner, fiefs and rule, with no new clan, party or gold), `banner` (the new house's own code; Gondor flies it while the house rules), `banner_of`, fiefs, gold, relation, `influence` (the restore price, 2500), `former_banner` |
| `<StartingClaims>`, `<Chronicle>` | Claims at campaign start; how many past holders each title remembers |

The hierarchy is made once, when a campaign first gets titles. Names, styles, prices and the settings sections are read on each campaign load. Unions, titular realms, offices and starting claims missing from a save are added on its next load.

### Titular realms

`<TitularRealms><Realm>` makes a kingdom title of no realm, vacant at the start, over another realm's lands: Khazad-dûm (`k_khazad_dum`) lies over the orcs' Moria (`lands="k_mistymountainorcs"`) under Durin's Folk (`liege`). While vacant it takes no titles from that realm; the tree draws that realm's vassal titles under it, dimmed, and its page lists them as the lands it lies over. Once formed, those titles move under it (`RealmsService.SyncTitularLands`, run on forming, after every title change, daily and on load): its holder is their liege, and the orcs' kingdom title is left with no lands until Khazad-dûm falls vacant again, when they move back. The moved tree is saved. Forming or pressing it needs more than half of the counties below it (below `lands` while vacant), and only heroes of a `cultures` culture may (refused with "Only its own people can take up this title."), so the orcs who hold Moria never form it. A missing `lands` title skips the realm with a warning; a missing `liege` leaves it without one.

### Realm names

A kingdom is named after the highest Kingdom, High or Union title its ruler holds above its own kingdom title: Erebor reads Durin's Folk while its king holds that title, Gondor reads Reunited Kingdom under the union. A title beside its own (Mordor holding Arnor, a Steward forming Arnor) does not rename the realm. A kingdom made in the campaign, with no title of its own, takes its ruler's highest title that is no other realm's own. The name and informal name come from the title's `realm_name` attribute (on `<HighTitle>`, `<Union>` and `<Realm>`), or its own name; the formal name is the title's full name ("Mansion of Khazad-dûm"). The kingdom's own names are kept in the save and come back when the title is lost. Names are set again on each load, daily, when a ruling clan changes and after a title changes hands; only the co-op host renames.

### Tribute

Each day a clan pays its liege clans a share of its income from fiefs: town tax, tariffs, projects and village income, the same sums as the base game's Settlement Income line. Its liege clans are those whose lords hold the direct liege title of a title its lords hold, from `tribute_rank` (Barony) up and within the same realm. Lordships owe nothing, nor does a clan to itself, and a clan in mercenary service neither pays nor receives. A clan with more than one liege clan splits its share evenly. Tribute received is not income from fiefs, so it never passes further up: as king of Gondor Aragorn is paid by the dukes of its duchies, and the counts pay their duke, not him. A clan whose gold is below the day's tribute pays none that day, and its lieges receive none from it.

Tribute is part of each clan's daily gold change (`TaomClanFinanceModel`), so the base game's daily tick moves it with the rest and there is no chat message. The map bar's expected gold change shows Tribute from Vassals and Tribute to Liege, each with one line per clan; the clan screen's income and expenses tiles carry them too. Receiving and paying have their own switch and percentage: with paying off a liege is still paid (by nobody), with receiving off a vassal still pays (to nobody). `<Rank tribute>` elements from older files are ignored.

### Mod options: World/Realms

The file sets the prices and amounts; the MCM group scales or switches them in play, with no restart (`RealmsSettingsProvider`, which clamps a hand-edited json to the slider range). Percentages apply to the player and the AI alike, and round so a positive price never drops to 0.

| Option | Range, default | Effect |
|---|---|---|
| Claim Price (%) | 10 to 500, 100 | `<Claims>` buy prices |
| Title Influence Cost (%) | 10 to 500, 100 | Usurp, form, restore and revoke influence, the coronation's own `influence` included |
| AI Lords Press Claims | on | Off stops the weekly AI claims; `<Claims ai="false">` still turns them off as well |
| Tribute From Vassals | on | Your clan receives a share of its vassal clans' income from fiefs, as every liege clan does |
| Tribute From Vassals (%) | 1 to 25, 5 | That share |
| Tribute To Liege | on | Your clan, and every vassal clan, pays a share to its liege clans |
| Tribute To Liege (%) | 1 to 25, 5 | That share |
| Office Influence (%) | 0 to 300, 100 | Weekly influence of the offices |
| Resentment Over Fiefs | on | The weekly relation and loyalty loss when another clan owns your title's fief |
| Liege's Pull | on | The weekly relation gain toward the liege |
| Liege Army Discount | on | Half-price army calls for vassals |
| Lands for Sons and Daughters | on | Daily grants of lesser titles to grown children; the campaign-start grants stay |
| Lords Styled by Their Titles | on | Styled party and conversation names (party names refresh within an hour); the encyclopedia always shows titles. Presentation only, so left out of the co-op settings fingerprint |

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/Realms/RealmsModule.cs` | Feature module: the behavior, the null popup adapter, the PartyNames patch |
| `Main/Features/Realms/RealmsCampaignBehavior.cs` | Campaign entry point: listeners and save data |
| `Main/Features/Realms/RealmsCampaign*.cs` | Coordinator: events, claims, feudal actions, coronation |
| `Main/Features/Realms/Domain/Services/RealmsService.cs` | Rules facade |
| `Main/Features/Realms/IRealmsPopupAdapter.cs` | The coronation window seam |
| `Main/Features/Realms/RealmsSettingsProvider.cs` | The World/Realms mod options, clamped (`IRealmsSettingsProvider`) |
| `Main/Features/Realms/Cheats/RealmsConsole.cs` | Console commands |
| `Client/Features/Realms/RealmsClient.cs` | Client start and tick: conversation names, the Titles page patch, the UI queue |
| `Client/Features/Realms/Encyclopedia/*` | Title rows on the themed encyclopedia pages, the Titles category (`TitleEncyclopediaPage`) and the title page (`TitleEncyclopediaPageVM`) |
| `Client/Features/Realms/Popups/*` | Title tree, claimants and coronation windows |
| `Main/_Module/GUI/PreFabs/Realms/*.xml` | The title page (`RealmsEncyclopediaTitlePage.xml`) and the popup prefabs (placeholder art) |

## Dependencies

- `ICoopSessionProvider` (CoopInterop): the host-only gate
- `IRealmsPopupAdapter`: the coronation window, from TAOM.Client
- TAOM's themed encyclopedia prefabs (`TAOMEncyclopediaSettlementPage`, `TAOMEncyclopediaHeroPage`, `TAOMEncyclopediaFactionPage`): the prefab extensions insert at `OwnershipColumn`, `InfoContainer` and `DescriptionList`

## Tests

- `TAOM.Tests/Features/Realms/`: the domain suite carried over from TAOM.Realms (catalog, generation, succession, claims, feudal rules, family grants, persistence, prefixes, the tree layout, the shipped XML) and `RealmsCoopGateTests` (a co-op client creates no titles). `RealmsSettingsProviderTests` pins the mod options: defaults, clamps, slider ranges, hints, the percentage scaling and the co-op split.
- `ClientIsolationGateTests.TaomClient_DeclaresEveryUiExtension` names the six Realms encyclopedia extensions.
- Co-op row: the "Gated (host-only)" table in `docs/features/coop-interop.md`.

## How to verify in game

**Preconditions:** TAOM built with this change, the separate TAOM.Realms module **disabled** (both would register the behavior and the patches), cheat mode on.

1. New campaign, wait one in-game hour, then `taom.realms_titles`. Pass: a tree of kingdom titles with holders and a total line; the log has `[TAOM.Realms] created N titles.`
2. `taom.realms_title town_EW1` (Minas Tirith). Pass: holder, laws and Gondor's realm title as de jure liege.
3. Open a settlement, hero and kingdom page in the encyclopedia. Pass (visual, a person checks): the Title, Titles and realm title sections show.
4. Encyclopedia, Titles, then a Gondor duchy (for example the Duchy of Anórien). Pass (visual): its page shows the banner, name and rank, the holder's portrait, the seat with its owner, the liege title, the vassal titles and the actions with their price or refusal reason. Clicking the liege or a vassal opens that title's page.
5. Open the title tree from any title section and click a card. Pass: the tree closes and that title's page opens (not the settlement or hero).
6. With a clan in another realm and enough resource, open the page of a foreign duchy next to your lands. Pass: Buy a claim is refused with "You, your clan or your vassals must first hold one of its lands." Take one of its towns and open it again. Pass: Buy a claim is offered; buy it: a confirmation, then the page rebuilds with your claim counted. The realm above it is still refused until you hold the duchy. In your own realm the action is refused with its reason.
7. Load a save made with the separate TAOM.Realms module. Pass: `taom.realms_hero` lists the same titles as before.
8. As ruler of your own kingdom, give Minas Tirith and West Osgiliath to two vassal clans, buy a claim on the Duchy of Anórien and open its page. Pass: Usurp is offered for 200 influence; before the fiefs were given away it was refused with "You, your clan and your vassals must hold more than half of its lands."
9. Serve Gondor with Aragorn in your clan and open the Kingdom of Gondor's title page. Pass: Restore costs 2500 influence; at Title Influence Cost 50% it costs 1250. Restore it. Pass (visual): the House of Telcontar and the Kingdom of Gondor show the Telcontar banner, on the clan page, the kingdom page and the map parties. Save, load. Pass: Gondor keeps that banner.
9b. Play as Aragorn (Player Switcher or the faction screen), join Gondor as a vassal or mercenary, gather 2500 influence and open the Kingdom of Gondor's title page. Pass: Restore warns "Your clan will become the House of Telcontar and rule Gondor from Minas Tirith." Restore it. Pass: your clan is named House of Telcontar, rules Gondor and holds Minas Tirith and Anórien, Gondor flies the Telcontar banner, Denethor stays Steward, and a mercenary contract has ended. Save, load. Pass: the banner and the name hold.
10. As Dáin, form or take Durin's Folk. Pass: the kingdom reads Durin's Folk on the map, the kingdom page and the encyclopedia list; lose the title and it reads Erebor again. Save and load in between. Pass: the names hold.
11. As a dwarf ruler, take two of the three Moria towns and open the Kingdom of Khazad-dûm title page. Pass: Form is offered, and the title tree shows Zirakzigil, Caradhras and Fanuidhol dimmed under it. Form it. Pass: the three duchies are drawn under Khazad-dûm undimmed, the orcs' kingdom title page (`k_mistymountainorcs`) lists no vassal titles, and save and load keep it so. An orc lord holding the same towns is refused with "Only its own people can take up this title."
12. Mod options, World/Realms: turn off Lords Styled by Their Titles. Pass: within an in-game hour Denethor's party reads "Denethor's Party", and a conversation with him shows the plain name; the encyclopedia still lists his titles. As Denethor, or any lord with vassal clans, hover the gold on the map bar. Pass: Tribute from Vassals lists each vassal clan with its amount, and the next day's gold change matches the tooltip. Set Tribute From Vassals (%) to 10. Pass: the amounts double at once. Turn off Tribute From Vassals. Pass: the line is gone. As a vassal, Tribute to Liege shows on your tooltip and your expenses tile.

None of these has been run yet.

## How to add or rename titles

1. Edit `realms_titles.xml`: a `<Prefix>` renames titles of a realm, region or place; a `<Duchy>` region lists its towns and castles.
2. Names and prices apply on the next campaign load. Hierarchy changes apply to new campaigns only.
3. No code changes needed.

## Performance

Titles are created once, on the first hourly tick. The hourly listener is a flag check after that. Claims, offices and consequences run weekly, grants to children daily. Tribute is a sheet built once per in-game day, or again when a tribute percentage changes: one pass over the titles and the fiefs of the clans in it. Every gold-change call, the map bar tooltip and each clan's daily payment, is then a lookup. The client tick drains a small UI queue. A title page builds its rows once when it opens and again after an action.

The weekly tick is one sweep over the AI clans that own a town or castle. Forming a title weighs every vacant duchy and realm against the clan's side (its vassals found through the whole title tree), which asks after heroes many thousands of times, and the game's `Hero.Find` walks every hero the campaign ever had. So during the sweep `ClaimWorld` finds heroes in a table built once (clan, realm and life are still read live), a clan that cannot pay the cheapest formation or usurpation is not quoted at all (it would stop at the first offer), one side serves a clan's whole usurp offer list, the ledger keeps each holder's titles in an index, and only the parties of heroes who gained or lost a title or office have their names rebuilt. Each weekly tick logs its time per part (`[TAOM.Realms] weekly tick ... ms`).

## Changelog

- 2026-11-12: playing as Aragorn, restoring Gondor's throne crowns you: your own clan is renamed House of Telcontar and becomes the ruling house, with Minas Tirith, Anórien and the Telcontar banner, and a mercenary contract ends. Before, the player hero was left out of the coronation and held the crown in name only while Denethor's house still ruled.
- 2026-11-12: tribute is a daily share of the vassal clan's income from fiefs, 5% by default, instead of a weekly percentage of a fixed amount per rank (Denethor drew about 100 gold a week). It is part of each clan's gold change, shown in the map bar's expected change and the clan screen instead of a chat message. Four mod options replace Tribute (%): Tribute From Vassals and Tribute To Liege, each with a percentage from 1 to 25. `<Consequences tribute_rank>` replaces the per-rank `<Rank tribute>` amounts. Host only in co-op.

- 2026-11-11: less work in the weekly AI claims, which froze the map (about 3 s on one logged week). Heroes are found from a table during the weekly sweep instead of `Hero.Find`, unaffordable clans are not quoted, usurp offers share one side, titles held are indexed and only affected party names are rebuilt. The weekly tick logs its timings.

- 2026-11-10: merged into TAOM from the separate TAOM.Realms module. Console commands renamed from `realms.*` to `taom.realms_*`; host-only co-op gate added; the coronation window moved behind `IRealmsPopupAdapter`.
- 2026-11-10: the encyclopedia's Titles category also shows on a new campaign. The home list keeps only the pages relevant when the map screen builds it, before the first hourly tick creates the titles; the page is now relevant while Realms runs and its list reads the titles each time it opens.
- 2026-11-10: every title has its own encyclopedia page, with the holder, the seat and its owner, the liege and vassal titles, claims, laws, offices, the chronicle and the player's actions (buy a claim, usurp, grant, revoke, form, restore, appoint, dismiss). Duchies and high titles, which have no settlement of their own, can now be claimed from it. Title links (tree cards, liege, vassals, the titles a hero holds or claims, the title name on settlement and kingdom pages) open that page instead of the settlement, kingdom or holder. The Ctrl+T titles window is removed; the encyclopedia's Titles category replaces it.

- 2026-11-10: your side counts. Usurping or forming a duchy or higher counts the lands held by the hero, their clan and their vassals at any depth (lords of the same realm below them in the title tree); a ruler counts every lord serving the realm. A town or castle title also counts while one of them owns its fief, so fiefs handed to vassal clans count before their titles are pressed. A ruler buys claims bordering any town or castle of the realm, not only the clan's (`RealmsService.Allegiance.cs`, `ClaimWorld.BuyerOf`). AI rulers now form vacant duchies their lords hold.
- 2026-11-10: lord names lose the titles written into them (Denethor II, Imrahil, Galadriel, Celeborn, Forlong, Hirluin, Angbor, Golasgil, Duinhir, Gimilzôr, Vargûl), in `lords.xml`, `lords.xslt`, their string files and every translation. Realms styles them from what they hold, so "Steward Denethor of Gondor" no longer reads "Denethor II, Steward of Gondor" twice over.
- 2026-11-11: World/Realms mod options: claim price, title influence cost, tribute and office influence as percentages, and switches for AI claims, resentment, the liege's pull, the army discount, grants to children and title styles. The coronation's `banner` gives the House of Telcontar its own banner; because a ruling clan shows its kingdom's banner, Gondor takes it too, and it is set again on each load (TAOM's banner injection resets kingdom banners on load). Restoring Gondor's throne for Aragorn costs 2500 influence (was 1000).
- 2026-11-11: titles are climbed. A claim on a duchy or higher is only for sale once the buyer, their clan or their vassals hold one of its vassal titles (or own the fief of one): a county first, then the duchy, then the realm (a Rivendell lord bought a claim on the Dominion of the Misty Mountain Orcs from a castle that merely bordered one of theirs). A bought claim on a vacated duchy or higher is taken up only when its claimant could press it; blood and conquest claims are unchanged (`RealmsService.Claims.cs`, `ClaimRefusal.NoFoothold`). Claims bought before this stay, and still need the lands to be pressed.

## GitHub Issue

- **Issue:** none yet
- **Status:** to be created
