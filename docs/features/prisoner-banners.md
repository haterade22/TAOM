# Prisoner Banners

## Overview

On the party screen, the row of a captive hero shows who the prisoner is at a glance: the clan's own banner at the top left of the portrait, the kingdom's banner at the top right (when the clan has a kingdom), and a gold noble icon at the bottom left when the hero leads the clan. It is read-only: no Harmony patch, no campaign behavior, nothing saved.

## Why This Exists

- **Vanilla behavior:** a hero prisoner's row shows a portrait and a name. Which clan or kingdom the lord belongs to, and whether the lord leads the clan, is not visible without opening the encyclopedia.
- **TAOM requirement:** a player deciding whom to ransom, recruit or execute wants the allegiance and rank on the row (#902).
- **Without this feature:** the player opens each prisoner's encyclopedia page to learn the same thing.

## What the player sees

On every hero prisoner row, on either side of the party screen (the left and the right tuple):

- **Clan banner:** `Clan.ClanOriginalBanner`, top left of the portrait.
- **Kingdom banner:** `Kingdom.Banner`, top right, only when the clan has a kingdom.
- **Noble icon:** the vanilla sprite `General\Mission\noble`, tinted gold (`#FFD700`), bottom left, only when `hero.IsClanLeader`.

Nothing is added to a non-hero row, a row that is not a prisoner, a hero with no clan, or any row while the MCM toggle is off. The talk icon keeps vanilla's tint, and no hover hint is added.

## Rules

`PrisonerBannerRules.Decide(enabled, isPrisoner, hasClan, hasKingdom, isClanLeader)` returns the three flags. Only an enabled prisoner with a clan gets anything (the mixin takes the clan from `Character.HeroObject`, so a non-hero row has none); the kingdom banner also needs a kingdom and the icon also needs the leader.

A hero row has no tier icon (`CharacterStatsModel.GetTier` is 0 for a hero, so `CampaignUIHelper.GetCharacterTierData` returns an empty sprite) and no type icon (`GetCharacterTypeData` returns an empty sprite for a hero), so both top corners are free. Both banner view models are built nine-grid (`new BannerImageIdentifierVM(banner, nineGrid: true)`), which is what the vanilla brush `Flat.Tuple.Banner.Small.Hero` is paired with. A mercenary clan shows its employer's kingdom banner, because mercenary service sets `Clan.Kingdom`.

**Known limit:** after an execution on the screen, the other rows do not refresh. If the new clan leader is another prisoner, his icon appears only when the screen reopens, and a destroyed kingdom's banner stays until then.

## Configuration

| Field | Value |
|-------|-------|
| Group | "World/Prisoner Banners", `GroupOrder = 75` |
| Name | "Show Prisoner Banners" (`PrisonerBannersEnabled`) |
| Default | on (`PrisonerBannersSettingsProvider.DefaultEnabled`) |
| Restart | not required; read each time a row refreshes, so it applies the next time the party screen opens |

The setting is new, so the default reaches every install. **Never flip the default** later: MCM's json2 keeps a stored value; rename the property instead. Under co-op the switch is classified as presentation only (`CoopSettingsRelevance`), since it changes what this client draws.

## Architecture

- **Core (`Main/`):** `PrisonerBannerRules` (pure), `PrisonerBannersSettingsProvider` (one cached settings reference, on before MCM is up), and `PrisonerBannersModule`, a feature module that registers only the provider (`OwnsSaveData` is false).
- **Client (`Client/`):** `PrisonerBannerMixin`, a bare `[ViewModelMixin]` on `PartyCharacterVM` (no refresh hook), found by UIExtenderEx's assembly scan, so there is no line in `TaomClientComposition.cs`. UIExtenderEx builds it as the row's constructor returns, when `Character`, `IsHero` and `IsPrisoner` are already set, so it reads everything once in its own constructor, as the encyclopedia unit badge mixin does. It adds `HasPrisonerClanBanner`, `PrisonerClanBanner`, `HasPrisonerKingdomBanner`, `PrisonerKingdomBanner` and `IsPrisonerClanLeader`. A throw clears all five and is logged once per exception type.
- **Prefabs:** TAOM already ships clones of both tuples, so the tuples are edited directly rather than through a `PrefabExtension`. Each carries a marker comment and one `<TaomPrisonerBanners />` tag inside `Party.TroopBack`, after the type icon. The three widgets live once in `Main/_Module/GUI/PreFabs/Party/TaomPrisonerBanners.xml`. Each banner is a `MaskedTextureWidget` with the vanilla brush `Flat.Tuple.Banner.Small.Hero`, bound to a nine-grid `BannerImageIdentifierVM`, inside a wrapping `Widget` that carries `IsVisible` (a data source widget's own `IsVisible` would bind inside the data source).

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/PrisonerBanners/PrisonerBannerRules.cs` | The three flags |
| `Main/Features/PrisonerBanners/PrisonerBannersSettingsProvider.cs` | The MCM switch |
| `Main/Features/PrisonerBanners/PrisonerBannersModule.cs` | Registers the provider |
| `Client/Features/PrisonerBanners/UI/PrisonerBannerMixin.cs` | The view model mixin |
| `Main/_Module/GUI/PreFabs/Party/TaomPrisonerBanners.xml` | The two banners and the noble icon |
| `Main/_Module/GUI/PreFabs/Party/PartyTroopTuple.xml`, `PartyTroopTupleLeft.xml` | The marker comment and the include inside `Party.TroopBack` |
| `Main/Features/TaomSettings.cs` | `PrisonerBannersEnabled` |

## Re-sync note

Both tuple copies carry the comment `<!-- TAOM #902 prisoner banners (TaomPrisonerBanners.xml): reapply after a re-sync to vanilla -->` and the line `<TaomPrisonerBanners />` right after the `TypeIconData` widget inside the `BrushWidget Brush="Party.TroopBack"`. After replacing either file with a fresh vanilla copy, reapply both lines. `PrisonerBannersUiWiringTests` fails when an include is missing, duplicated or misplaced.

## Co-op

Read-only UI with nothing saved, so it is not gated. The mixin registers under co-op too, since it only reads the hero, clan and kingdom the party screen already shows. The setting is player-local: `CoopSettingsRelevance` lists `PrisonerBannersEnabled` as presentation, so it is not part of the settings fingerprint. See "Deliberately NOT gated" in [coop-interop.md](coop-interop.md).

## Dependencies

- UIExtenderEx 2.13.3, whose assembly scan finds the mixin.
- The vanilla brush `Flat.Tuple.Banner.Small.Hero` (Native `Brushes/Standard.xml`) and the sprite `General\Mission\noble` (Native `NativeSpriteData.xml`).
- TAOM's two tuple clones, `PartyTroopTuple.xml` and `PartyTroopTupleLeft.xml`, and the new sub-prefab `TaomPrisonerBanners.xml`.
- `TaomSettings` (MCM): the switch.

## Known limits and look notes

- After an execution on the screen, the other rows do not refresh. If the new clan leader is another prisoner, his icon appears only when the screen reopens, and a destroyed kingdom's banner stays until then.
- A mercenary clan shows its employer's kingdom banner, because mercenary service sets `Clan.Kingdom`.
- A prisoner from a ruling clan shows the same banner in both corners, because a ruling clan's own banner takes the kingdom's colours. That is by design.

## Tests

- `TAOM.Tests/Features/PrisonerBanners/PrisonerBannerRulesTests.cs`: the flag matrix.
- `PrisonerBannersSettingsProviderTests.cs`: the default, the read-through to the settings, the group order, the restart flag and hint, the co-op classification.
- `PrisonerBannersUiWiringTests.cs`: the bare mixin attribute and no `OnRefresh`; every banner view model nine-grid; one include per tuple, inside the troop back after the type icon; every binding in the sub-prefab resolved by `PrefabBindingWalker`; the five exact tokens; each banner shown by its own flag; the brush, the sprite, the icon size and the inert flags; the talk icon unchanged; the noble sprite in the installed game; the module in `FeatureModules`.
- `TAOM.Tests/Features/CampaignHotPathSettingsProvidersTests.cs`: the provider's cached settings and its registration.
- `TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs`, `ServerSettings/ServerSettingsSchemaTests.cs` and `Infrastructure/ClientIsolationGateTests.cs`: the counts this feature moved by one.

## How to verify in game

**Preconditions:** a save holding at least one captive lord, ideally one who leads a clan in a kingdom and one in a clan without a kingdom; the MCM toggle "Show Prisoner Banners" on.

1. Open the party screen (P). On the prisoner list, the lord's row shows the clan banner at the top left and the kingdom banner at the top right of the portrait.
2. A clan leader's row also shows the gold noble icon at the bottom left; a non-leader's row does not.
3. A lord of a kingdomless clan shows only the clan banner.
4. Check both sides: transfer the lord between your prisoners and a settlement's dungeon (the left tuple), and look at the banners again.
5. Turn the toggle off, reopen the party screen: no banners or icon. Troop rows and non-prisoner heroes never show them.
6. Judge the look: corner offsets, the banner size next to the portrait, and the gold of the icon.

## Changelog

- 2026-10-27: first version (#902).

## GitHub Issue

- **Issue:** #902
- **Status:** Open until the in-game check
