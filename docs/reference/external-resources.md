# External Resources for TAOM

Curated, verified external references for improving TAOM (Bannerlord 1.4.5 LOTR total conversion). Compiled 2026-05-29 via web research with per-resource verification (fetched URLs; the docs hierarchy was personally re-checked to correct an official-vs-community mislabel). This is the high-value subset — not an exhaustive dump. Cite this node instead of re-searching.

> **Verification key:** ✅ = fetched/confirmed this session · 🔎 = strong search hit, not deep-fetched.

## Bannerlord modding — docs hierarchy (use in this order)

| Resource | Status | Use for |
|---|---|---|
| **[moddocs.bannerlord.com](https://moddocs.bannerlord.com/)** — *OFFICIAL (TaleWorlds)* | ✅ | The authoritative modding reference: asset management, XSLT usage, best practices, editor, audio, Workshop. **Canonical — prefer over the community mirrors.** |
| **[apidoc.bannerlord.com](https://apidoc.bannerlord.com/)** — *OFFICIAL API ref* | 🔎 | TaleWorlds.Core/CampaignSystem/Engine API surface. **⚠️ lags at v1.3.14** — for 1.4.5 keep using `pwsh tools/taom-src.ps1` + `/verify-bindings` (decompilation is our source of truth). |
| **[docs.bannerlordmodding.com](https://docs.bannerlordmodding.com/)** — *community* ([Bannerlord-Modding/Documentation](https://github.com/Bannerlord-Modding/Documentation)) | ✅ | Community C# API / Gauntlet / XML reference. Practical, but **not official** (the research initially mislabeled it "official" — it isn't). |
| **[docs.bannerlordmodding.lt](https://docs.bannerlordmodding.lt/modding/models/)** — *community* | ✅ | Has the clearest **GameModel decorator-pattern** write-up (wrap-previous-model, delegate, override) + localization notes. |
| **[BUTR ReferenceAssemblies docs](https://butr.github.io/Bannerlord.ReferenceAssemblies.Documentation/)** | 🔎 | DocFX API reference; sometimes more current than apidoc when TaleWorlds lags. |

## Bannerlord modding — dependencies & tooling

- **[github.com/BUTR](https://github.com/BUTR)** ✅ — home of TAOM's core deps: [Harmony](https://github.com/BUTR/Bannerlord.Harmony), [ButterLib](https://github.com/BUTR/Bannerlord.ButterLib), [MCM/MBOptionScreen](https://github.com/BUTR/Bannerlord.MBOptionScreen), [UIExtenderEx](https://github.com/BUTR/Bannerlord.UIExtenderEx), [ModuleManager](https://github.com/BUTR/Bannerlord.ModuleManager), [BLSE](https://github.com/BUTR/Bannerlord.BLSE). **Watch releases here for the next engine-bump early-warning** (see `docs/migration/dr3-maintenance.md`).
- **[Harmony docs](https://harmony.pardeike.net/)** ✅ — patch order, prefix/postfix/transpile, **finalizers are exception-immune** (use for fallback in GameModel/patch chains); prefer postfix + minimal scope for mod-compat.
- **[dnSpy](https://github.com/dnSpy/dnSpy)** ✅ / [ILSpy](https://github.com/icsharpcode/ILSpy) — runtime debugging of .NET 4.7.2 assemblies; complements our `ilspycmd` workflow for stepping patched methods / catching ABI drift.
- Community: [modding.wiki](https://modding.wiki/en/mountandblade2bannerlord) 🔎, the TaleWorlds modding forum, the BUTR/modding Discord.

## LOTR / Tolkien — authoring authenticity

- **[Tolkien Gateway](https://tolkiengateway.net/)** ✅ — canonical wiki; primary authority for faction/culture/settlement/character lookups (the culture→LOTR mapping work).
- **[Encyclopedia of Arda](https://encyclopedia-of-arda.com/)** ✅ + **[Arda Maps](http://arda-maps.org/)** 🔎 — names/pronunciation + interactive geography for authentic settlement placement.
- **Naming generators** ([RealElvish — Gondor](https://realelvish.net/naming/gondor/) ✅ · [Rohirrim](https://realelvish.net/naming/rohirrim/) ✅) — culturally-correct lord/NPC names (Sindarin vs Old-English; Rohirrim "echo the parent's name" rule). **Feed these into `/new-culture` + `/lord-skills`.**
- **Books**: *The Atlas of Middle-earth* (Karen Wynn Fonstad, ISBN 9780618126996) — maps incl. travel-days for settlement/distance authenticity; *The Complete Guide to Middle-earth* (Robert Foster, 2022, ISBN 9780008537814); *The Peoples of Middle-earth* (HoME Vol. 12) for Dúnedain/Gondor/Rohan ancestry; Tolkien's own *Guide to the Names in LOTR* (naming principles).
- ⚠️ **Skip** Ruth Noel's *The Languages of Tolkien's Middle-earth* — pre-Silmarillion, known errors. The `lotr.fandom.com` wiki is fan-driven — cross-check against Tolkien Gateway, don't treat as canon.

## Engineering / total-conversion design

- **Comparable mods to study** (scope/balance/roster mgmt): [Kingdoms of Arda](https://www.moddb.com/mods/a-lord-of-the-rings-mod-kingdoms-of-arda) 🔎 (closest LOTR analog), Enderal 🔎 (volunteer long-cycle TC).
- **[Game-mods semantic versioning](https://github.com/pragasette/game-mods-semver)** ✅ — *same major = save-safe; major bump = new game.* Worth formalizing as TAOM's save-compat contract (complements the `Save-compat:` commit trailer).
- **.NET perf**: reflection cost (compiled-delegate vs `MethodInfo.Invoke` break-even ~3,500 calls) → cache reflected accessors in per-tick GameModels; .NET 4.7.2 ZLib speedup for large XML loads.

## Known gaps (no good external resource exists)

- **No published v1.4.5 API docs** — decompilation (`taom-src` / `/verify-bindings`) is the source of truth. Already handled.
- **No Bannerlord-specific perf profiler/guide** — profile our own hot paths (GameModel ticks, Harmony per-frame patches, SpatialGrid) if/when a perf issue actually surfaces. Don't pre-build a harness.
- **No LOTR-mod-authoring resource library** — TAOM's own domain docs (`docs/ai-includes/new-culture-authoring.md`, etc.) are the asset; nothing external matches.

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/lord-skills-authoring.md](../ai-includes/lord-skills-authoring.md)
- [docs/ai-includes/new-culture-authoring.md](../ai-includes/new-culture-authoring.md)
- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
