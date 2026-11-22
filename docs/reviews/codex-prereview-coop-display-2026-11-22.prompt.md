# Codex pre-review: co-op display fixes (fix/coop-display)

Review-only. Do not edit files. Report findings with severity (P1 to P3), file:line and the evidence line.

Repo: E:/repos/taom-coop-display, branch fix/coop-display. Range: `git diff 3b588519c..HEAD` (3 commits by an
outside contributor). TAOM is a Bannerlord v1.5.7 total conversion, .NET Framework 4.7.2. BannerlordCoop is a
co-op mod: one server process is the authority, guests load the server's save and defer campaign time to it.

## The change

1. `fix(time-acceleration)`: `TimeAccelerationMixin` (UIExtenderEx mixin on `MapTimeControlVM`) loses its
   `[CoopSuppressedUi]` attribute, so it registers under co-op too. New `ITimeAccelerationService.IsExtraFastForwardAvailable`
   (`!_coop.ShouldDeferToHost`) hides the Extra Fast Forward button in the themed prefab `TAOMCampaignTime.xml`.
2. `fix(faction-ui)`: `GauntletLayerLoadMoviePatch` swapped the MapBar and TaomFieldCampOverlay movies only when no co-op
   module was present (`CoopPresence.IsActive`). Now `ThemedMapBarCoopPolicy.KeepsNativeMapBar(coop)` =
   `coop.ShouldDeferToHost && !coop.IsSessionActive` keeps the native bar only for a co-op mod TAOM cannot probe
   (BannerlordTogether).
3. `fix(banners)`: new Harmony prefix and finalizer on `Clan.PreAfterLoad` set a `[ThreadStatic]` flag; the existing
   prefix on private `Clan.UpdateBannerColorsAccordingToKingdom` returns false when `ShouldSkipLoadRepaint(InClanLoad)`
   (`insideClanLoad && _coop.IsCoopClient`). Goal: a co-op guest keeps the server's banners, because
   `BannerInjectionBehavior.OnGameLoaded` returns early on a non-authority peer and vanilla `Clan.PreAfterLoad` repaints
   kingdom clans from `Kingdom.PrimaryBannerColor` and `SecondaryBannerColor` (vanilla values in `spkingdoms.xslt`).

## Look hard at

- Timing: is `ICoopSessionProvider.IsCoopClient` already true while `Clan.PreAfterLoad` runs during a co-op join's
  save load? Read `Main/Features/CoopInterop/CoopSessionProvider.cs` and how it learns the session state. If it is
  false at that moment, fix 3 does nothing.
- Does BannerlordCoop's server or its host process ever report `IsCoopClient` true (it must keep vanilla behaviour)?
- Under BannerlordTogether: the mixin now registers; does anything in it write campaign state or throw without the
  themed prefab? Is `KeepsNativeMapBar` correct for solo (no co-op mod), the Coop server process, a Coop guest?
- `FactionUIPatchContext.Coop` resolves `ICoopSessionProvider` lazily from IoC inside a movie-load prefix: can it run
  before the container is built (front end, main menu MapBar never loads, but check), and does a throw there escape?
- Harmony: `[HarmonyPatch(typeof(Clan), "PreAfterLoad")]` on a protected override; finalizer semantics with `__state`;
  any other caller of `UpdateBannerColorsAccordingToKingdom` inside `PreAfterLoad` call chain (OnLoad paths) that
  should not be skipped.
- Installed engine source: `pwsh tools/taom-src.ps1 path Clan` gives the v1.5.7 file; quote lines.
- Tests in the range: do they test the real behaviour or only mirrors?

Write the report as markdown: summary, then findings by severity, then UNVERIFIED items.
