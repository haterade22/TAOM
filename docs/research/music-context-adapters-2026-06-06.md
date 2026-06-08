# Music Context Adapter Research - 2026-06-06

Scope: campaign and mission context snapshot adapters for the TAOM music system. This step adds only adapter seams that produce `MusicRouteSnapshot`; it does not add Harmony patches, campaign behaviors, mission behaviors, or playback hooks.

## Source Drop Runtime Contract

- `_handoff/MusicSystem_SourceDrop/src/TAOM_AudioPack/TaomSharedPlaybackOwner.cs:130-181` defines `CampaignSnapshot` as a no-audio feed-in snapshot with settlement/tavern/combat event booleans plus `StableCultureId`, `SettlementCultureId`, `SettlementId`, and sea state.
- `_handoff/MusicSystem_SourceDrop/src/TAOM_AudioPack/TaomSharedPlaybackOwner.cs:185-215` defines `MissionSnapshot` as a no-audio feed-in snapshot with `Active`, `IsSiege`, `IsBattle`, `IsTown`, `IsTavern`, `CultureId`, and `SceneId`.
- `_handoff/MusicSystem_SourceDrop/src/TAOM_AudioPack/TaomSharedPlaybackOwner.cs:1458-1463` classifies a mission snapshot only when it is active and one of siege, battle, town, or tavern is true.
- `_handoff/MusicSystem_SourceDrop/src/TAOM_AudioPack/TaomSharedPlaybackOwner.cs:1752-1781` resolves mission bucket priority as siege, battle, tavern, town, then world.
- `_handoff/MusicSystem_SourceDrop/src/TAOM_AudioPack/TaomSharedPlaybackOwner.cs:1823-1877` resolves campaign bucket priority as siege, battle, tavern/town while in settlement, then world after settlement-left or mission-ended reevaluation, then world fallback.
- `_handoff/MusicSystem_SourceDrop/src/TAOM_AudioPack/TaomSharedPlaybackOwner.cs:2173-2195` derives settlement, tavern, battle, and siege intent from the selected context before building route selection data.
- `_handoff/MusicSystem_SourceDrop/src/TAOM_AudioPack/TaomSharedPlaybackOwner.cs:2812-2831` chooses mission culture first from mission culture then stable campaign culture; for campaign town/tavern it prefers settlement culture, otherwise stable culture.
- `_handoff/MusicSystem_SourceDrop/docs/RUNTIME_BEHAVIOR.md:10-59` documents the `ModuleSounds/taom/<bucket>/<culture>/<track>.ogg` layout and neutral culture fallback.

## Decompiled v1.4.5 API Facts

Commands used `C:\Users\kane0\.dotnet\tools\ilspycmd.exe` against `A:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client`.

- `TaleWorlds.MountAndBlade.Mission` exposes `static Mission Current`, `string SceneName`, and combat flags `IsFieldBattle`, `IsSiegeBattle`, `IsSallyOutBattle`, and `IsNavalBattle`. The mission adapter may use those flags directly.
- `TaleWorlds.CampaignSystem.Campaign` exposes `static Campaign Current` and `MobileParty MainParty`. `MobileParty.MainParty` itself reads `Campaign.Current.MainParty`, so the concrete source checks `Campaign.Current` before touching main-party state.
- `TaleWorlds.CampaignSystem.Party.MobileParty` exposes `CurrentSettlement`, `MapEvent`, `MapFaction`, `BesiegedSettlement`, and `IsCurrentlyAtSea`.
- `TaleWorlds.CampaignSystem.IFaction` exposes `CultureObject Culture`.
- `TaleWorlds.CampaignSystem.Settlements.Settlement` exposes `Culture`, `IsTown`, `IsCastle`, `IsVillage`, `IsUnderSiege`, and `static CurrentSettlement`.
- `TaleWorlds.CampaignSystem.MapEvents.MapEvent` exposes `IsFieldBattle`, `IsRaid`, `IsHideoutBattle`, `IsSiegeAssault`, `IsSallyOut`, `IsSiegeOutside`, `IsBlockade`, `IsBlockadeSallyOut`, `IsSiegeAmbush`, `IsFinalized`, and `IsPlayerMapEvent`.
- `TaleWorlds.ObjectSystem.MBObjectBase` exposes `string StringId`; `CultureObject` inherits that path through `BasicCultureObject`.

## Design Consequences

- The pure adapters map primitive state DTOs into `MusicRouteSnapshot`; services still consume only the snapshot and never see TaleWorlds types.
- Concrete TaleWorlds sources live in `Main/Adapters` and are the only new files in this step that reference `TaleWorlds.*`.
- Mission town/tavern detection is not inferred from `Mission.SceneName`. The decompiled `Mission` API proves battle/siege flags, but not a safe public tavern/town mission classifier. This avoids the same class of scene-name false positive documented by SiegeDismount.
- Campaign tavern remains an explicit input on the primitive campaign state. The v1.4.5 public campaign surface checked here did not expose a safe non-reflection tavern signal, so the concrete source sets it false until a later researched hook supplies it.
