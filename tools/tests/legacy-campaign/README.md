# Legacy campaign boundary regressions

Run in a fresh PowerShell 7 process from the repository root:

```powershell
pwsh -NoProfile -File tools/tests/legacy-campaign/Run.ps1
```

`-RepoRoot <path>` can select a different source tree, including an unchanged
baseline. The script never builds or deploys the game module. It uses PowerShell's
bundled Roslyn compiler; it needs no game DLLs, network access or .NET SDK.
The recorded run used PowerShell 7.6.5 on Windows.

The fixture supplies small managed collaborators and replaces eight method-body
placeholders with the actual source bodies, parsed with Roslyn. It also imports
the actual subscription fields and initializers. No production logic is copied
into the fixture. Other callbacks and engine I/O are faked. The two XML checks
pin an explicit troop whitelist and the Body slot required by the installed
LOTRLOME_Armory `BodyArmor` item.

The checks cover navigation model parameters, route fallback, live-scene height
queries, a scene without a terrain heightmap, battle roster protection and resumed
desertion, skip states, repeated campaign subscriptions, late old-owner cleanup,
unrelated subscribers, singleton diagnostics and the two affected Uruk troops.

These are isolated boundary regressions. They do not validate native lifetimes,
the real campaign event dispatcher, troop appearance, .NET Framework 4.7.2
compilation or the full MSTest suite. They deliberately do not reproduce a native
access violation in the game process. See the validation note for runtime evidence.
