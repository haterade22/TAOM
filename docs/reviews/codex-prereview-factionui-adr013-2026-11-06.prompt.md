# Codex pre-review: FactionUI move behind the ADR-013 client seam (2026-11-06)

You are a read-only reviewer. Do not edit files. Report defects with file:line evidence and a severity
(CRITICAL, HIGH, MEDIUM, LOW). Missing engine evidence is UNVERIFIED, never assumed.

Scope: commit `0ca5364de` on branch `fix/factionui-adr013` (worktree `E:/repos/taom-factionui-adr013`),
diffed against its parent `97d8cb72b`. Run `git show --stat 0ca5364de` and read every changed file.

Intent: the faction UI merge put 19 files of themed campaign and encyclopedia UI in core `TAOM.dll`, though
they name client-only assemblies (GauntletUI, ViewModelCollection, UIExtenderEx). ADR-013
(`docs/adrs/013-*.md`) says a dedicated server loads core without those assemblies. The commit moves the 19
files to `TAOM.Client`, adds `ICampaignPresentationAdapter` with a null core version and a client version,
moves two tree mixins into the client's UI extension registration, applies the
`TAOM.EncyclopediaTroopNames` Harmony category from `Main/SubModule.cs` GameInit, and fixes data gates.

Known suspects, check each:
1. `FactionUIPatchContext.Presentation` resolves the adapter lazily from IoC on the first movie load. Can it
   resolve before the client composition replaces the null registration, and cache the null version for the
   process? Read `Client/TaomClientComposition.cs` and when `ClientSubModule` registers.
2. `GauntletLayerLoadMoviePatch` casts `(ViewModel)boundSource!`. What if the adapter returns null or a
   non-ViewModel? Is the cast inside the try?
3. The troop-name patches now apply at the first game init instead of the first themed encyclopedia movie.
   They are gated on `EncyclopediaTroopNames.Active`. Is that flag ever reset (new campaign, return to main
   menu, a second campaign in one process)? Is any patched method's behaviour changed while Active is false?
4. The tree mixins now register at the client's OnGameStart with every other mixin, not under a second
   UIExtender created lazily. Do they now affect vanilla (non-themed) screens? Does co-op presence filtering
   (`Client/ClientUiExtensions.cs`) still apply to them as intended?
5. `EncyclopediaPictureCache.ReleaseAll` moved from core's `OnSubModuleUnloaded` to the client's unload. Is
   the order versus IoC teardown and texture release safe?
6. Nullable fixes in the 19 moved files: did any change behaviour (a `?.` that skips work, a `??` default)?
7. Prefab edits: `TAOMMapBar.xml` lost a VisualDefinitions block, `TAOMEncyclopediaUnitPage.xml` lost
   `MouseScrollAxis`. Does anything still name the removed definitions?
8. Any core method that still names a client-only assembly type (`ClientAssemblies.cs`).
9. The new allowlist row in `TAOM.Tests/Features/CoopInterop/CoopPresenceReadGateTests.cs` and the count
   change in `ClientIsolationGateTests.cs`: is each justified by the code?

Output: a list of findings with severity, file:line, the failure scenario, and evidence. End with a one-line
verdict.
