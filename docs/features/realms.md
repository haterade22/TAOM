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
| `<Consequences>` | The weekly resentment, liege pull and tribute |
| `<Coronations>` | Aragorn's coronation: house, banner, fiefs, gold, relation, `former_banner` |
| `<StartingClaims>`, `<Chronicle>` | Claims at campaign start; how many past holders each title remembers |

The hierarchy is made once, when a campaign first gets titles. Names, styles, prices and the settings sections are read on each campaign load. Unions, offices and starting claims missing from a save are added on its next load.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/Realms/RealmsModule.cs` | Feature module: the behavior, the null popup adapter, the PartyNames patch |
| `Main/Features/Realms/RealmsCampaignBehavior.cs` | Campaign entry point: listeners and save data |
| `Main/Features/Realms/RealmsCampaign*.cs` | Coordinator: events, claims, feudal actions, coronation |
| `Main/Features/Realms/Domain/Services/RealmsService.cs` | Rules facade |
| `Main/Features/Realms/IRealmsPopupAdapter.cs` | The coronation window seam |
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

- `TAOM.Tests/Features/Realms/`: the domain suite carried over from TAOM.Realms (catalog, generation, succession, claims, feudal rules, family grants, persistence, prefixes, the tree layout, the shipped XML) and `RealmsCoopGateTests` (a co-op client creates no titles).
- `ClientIsolationGateTests.TaomClient_DeclaresEveryUiExtension` names the six Realms encyclopedia extensions.
- Co-op row: the "Gated (host-only)" table in `docs/features/coop-interop.md`.

## How to verify in game

**Preconditions:** TAOM built with this change, the separate TAOM.Realms module **disabled** (both would register the behavior and the patches), cheat mode on.

1. New campaign, wait one in-game hour, then `taom.realms_titles`. Pass: a tree of kingdom titles with holders and a total line; the log has `[TAOM.Realms] created N titles.`
2. `taom.realms_title town_EW1` (Minas Tirith). Pass: holder, laws and Gondor's realm title as de jure liege.
3. Open a settlement, hero and kingdom page in the encyclopedia. Pass (visual, a person checks): the Title, Titles and realm title sections show.
4. Encyclopedia, Titles, then a Gondor duchy (for example the Duchy of Anórien). Pass (visual): its page shows the banner, name and rank, the holder's portrait, the seat with its owner, the liege title, the vassal titles and the actions with their price or refusal reason. Clicking the liege or a vassal opens that title's page.
5. Open the title tree from any title section and click a card. Pass: the tree closes and that title's page opens (not the settlement or hero).
6. With a clan in another realm and enough resource, Buy a claim on a duchy from its page. Pass: a confirmation, then the page rebuilds with your claim counted. In your own realm the action is refused with its reason.
7. Load a save made with the separate TAOM.Realms module. Pass: `taom.realms_hero` lists the same titles as before.
8. As ruler of your own kingdom, give Minas Tirith and West Osgiliath to two vassal clans, buy a claim on the Duchy of Anórien and open its page. Pass: Usurp is offered for 200 influence; before the fiefs were given away it was refused with "You, your clan and your vassals must hold more than half of its lands."

None of these has been run yet.

## How to add or rename titles

1. Edit `realms_titles.xml`: a `<Prefix>` renames titles of a realm, region or place; a `<Duchy>` region lists its towns and castles.
2. Names and prices apply on the next campaign load. Hierarchy changes apply to new campaigns only.
3. No code changes needed.

## Performance

Titles are created once, on the first hourly tick. The hourly listener is a flag check after that. Claims, offices, consequences and tribute run weekly, grants to children daily. The client tick drains a small UI queue. A title page builds its rows once when it opens and again after an action.

## Changelog

- 2026-11-10: merged into TAOM from the separate TAOM.Realms module. Console commands renamed from `realms.*` to `taom.realms_*`; host-only co-op gate added; the coronation window moved behind `IRealmsPopupAdapter`.
- 2026-11-10: the encyclopedia's Titles category also shows on a new campaign. The home list keeps only the pages relevant when the map screen builds it, before the first hourly tick creates the titles; the page is now relevant while Realms runs and its list reads the titles each time it opens.
- 2026-11-10: every title has its own encyclopedia page, with the holder, the seat and its owner, the liege and vassal titles, claims, laws, offices, the chronicle and the player's actions (buy a claim, usurp, grant, revoke, form, restore, appoint, dismiss). Duchies and high titles, which have no settlement of their own, can now be claimed from it. Title links (tree cards, liege, vassals, the titles a hero holds or claims, the title name on settlement and kingdom pages) open that page instead of the settlement, kingdom or holder. The Ctrl+T titles window is removed; the encyclopedia's Titles category replaces it.

- 2026-11-10: your side counts. Usurping or forming a duchy or higher counts the lands held by the hero, their clan and their vassals at any depth (lords of the same realm below them in the title tree); a ruler counts every lord serving the realm. A town or castle title also counts while one of them owns its fief, so fiefs handed to vassal clans count before their titles are pressed. A ruler buys claims bordering any town or castle of the realm, not only the clan's (`RealmsService.Allegiance.cs`, `ClaimWorld.BuyerOf`). AI rulers now form vacant duchies their lords hold.

## GitHub Issue

- **Issue:** none yet
- **Status:** to be created
