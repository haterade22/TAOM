# Bannerlord 1.4.8 campaign fixes: contribution evidence

## Scope and provenance

Target branch: `bannerlord-1.4.5`, based on
`ccef172adced5e25ba78f47bb25250006976e7ed` (source module version v2.0.28).
Runtime investigation used Windows, Bannerlord 1.4.8.119303 and TAOM 2.0.29.5.
This proposal ports the narrowly scoped local runtime fixes into the legacy
source branch. It does not change the module version or save schema.

The default development branch is now `bannerlord-1.5.x`. Its source at
`6589bdd110563edf18d7f26ac1e147c9f2573057` already corrects the Uruk equipment
slots and addresses the screen subscriptions. Those portions of this proposal
are backports, not new discoveries. The resource subscriber here tracks its
owning behavior so late cleanup cannot clear another campaign's ownership.

## Reproduction and changes

1. **Supply order from another settlement:** ordering supplies from a city other
   than the player's current settlement caused a native access violation in the
   path query. `ComputeNavPathPoints` supplied null terrain exclusions and fixed
   100/100 region costs. Use the active navigation and distance models instead,
   preserving their non-naval zero costs as well as the existing endpoint fallback.
   The player repeated the order after the local fix and confirmed it worked.
   This validates the combined parameter change; it does not isolate which invalid
   argument was necessary to trigger the native failure.
2. **Return from a siege ambush:** returning from a defensive ambush to the siege
   menu crashed while supply-route arrows were synchronized. The local dump
   identified the terrain-height path and a null native terrain pointer. Query
   the live arrow scene only after `HasTerrainHeightmap`; otherwise use the land
   collision query and an upright normal. The mask is `Moveable (16) |
   OnlyCollideWithRaycast (128)`, verified against installed 1.4.8 engine metadata
   and the land-height call path. The player repeated the ambush and returned to
   the siege menu without a crash, then saved and exited normally.
3. **Alignment desertion, related to #750:** skip field parties and garrisons
   while their `MapEvent` is active, following the native desertion boundary.
   Normal daily desertion resumes after the map event. This is a specific roster
   protection fix, not proof that every stuck-battle report in #750 is resolved.
4. **Screen subscriptions, related to #771:** replace the resource behavior's
   previous global subscriber on each registration. Old cleanup removes only its
   own subscription. Remove before adding the diagnostics singleton's handler.
   This bounds these subscribers, not all possible campaign memory retention.
5. **Uruk equipment, related to #729:** place `sk_uruk_hai_skirt_a1` in Body for
   `urukhai_champion` and `urukhai_berserker`, matching its installed BodyArmor
   type. No item stats change; armor and weight can now contribute as intended.

## Verification and limits

- The two native crash fixes were individually retested in the installed game
  by the reporting player, as described above.
- After all five local fixes, the player loaded the existing save and opened
  the party/troop screen successfully. This is a smoke test, not a full gameplay
  regression pass. The installed fixes were applied to the shipped assembly;
  this source tree has not been built and deployed as a replacement module.
- An earlier managed IL harness exercised the installed versions: 18 checks,
  10 failing before the three newer fixes and 18 passing afterward.
- `tools/tests/legacy-campaign/Run.ps1` compiles the eight current source method
  bodies against managed fakes and verifies the two XML equipment assignments.
  Against the unchanged base: 44 behavioral checks, 25 passing and 19 failing;
  both XML checks fail. With the fixes: 44/44 behavioral and 2/2 XML checks pass.
- Engine signatures and relevant behavior were checked locally against the
  installed 1.4.8 assemblies. The harness uses PowerShell 7's runtime, not the
  target Framework runtime. It cannot prove engine binding or native safety.
- Full solution build and MSTest: **not run**, because the .NET SDK is absent.
  With the normal development dependencies installed, use the non-deploying
  commands in `.ai/verification.md`.
- Still outstanding: gameplay coverage of desertion across a battle boundary,
  repeated campaign reloads and resource spending, Uruk armor in a live mission,
  and any port/build/runtime validation on Bannerlord 1.5.x.
- The initial submission was self-reviewed; the subsequent maintainer review is
  recorded below. No maintainer acceptance or merge is claimed. No game binaries,
  saves, raw logs, crash dumps or private paths are included in this contribution.

## PR #1064 review follow-up (2026-10-30)

[Haterade's review](https://github.com/haterade22/TAOM/pull/1064#pullrequestreview-5712895030)
identified a compile-time mismatch missed by the initial signature checks:
`Scene.GetHeightAtPoint` takes `ref float height`, whereas both the source call
and the harness fake used `out`. The production call and fake now use `ref`.
The initialized height, terrain guard, collision mask and fallback behavior are
unchanged.

- Changing only the fake to `ref` first made `Run.ps1` fail with **CS1620** and
  exit 1. After correcting the production call, the same command passed all
  **44 behavioral + 2 XML checks**, exit 0.
- Reflection of the installed 1.4.8 `TaleWorlds.Engine.dll` confirmed that the
  third parameter is `System.Single&`, with `IsOut == false`. Its SHA-256 is
  `BFD5DB82D92F75EF15A5EB9A9F0C9391412566933193122DB1684019ED704D68`.
- A separate minimal call compiled with PowerShell 7's Roslyn and references to
  the installed `TaleWorlds.Engine`, `TaleWorlds.Library` and `TaleWorlds.DotNet`
  assemblies rejected `out height` with **CS1620** and accepted `ref height`.
  The probe methods and native engine methods were not invoked.
- Full solution build and MSTest remain **not run**: the .NET SDK is unavailable.
  The signature probe does not replace a .NET Framework 4.7.2 solution build or
  gameplay validation. No module was deployed and no new gameplay test was run.
