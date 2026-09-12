**TAOM changelog: 29 Aug to 12 Sep**
Released as v2.0.24 to v2.0.28. "Next build" items are done and in testing.

**Released**
• Enlisted soldiers now get weapons with their kit. All 84 quartermaster kits were armour only.
• AI army sizes now ship at base game values; bigger AI armies are opt-in in Mod Options. Lords no longer spawn with 2,000+ men on day one, and six Mordor troop types lost in that resize are back.
• A phantom career was eating your first career point on every new campaign. Fixed.
• The career ability key can be rebound. It was hardcoded to V.
• Play as a Lord: picking a lord skips the six backstory menus, and taking over a lord no longer kills every tooltip in the game.
• Umbar troops and lords were fighting in Calradian rags. Dressed.
• Two-handed troops no longer carry a shield the AI could never use (115 removed). Lindon and Noldor recruits no longer spawn with a bow and no arrows.
• Crash when hovering a troop stack on the party screen. Fixed.
• Dead horses fade 5 seconds after death instead of littering the field (toggle and slider in Mod Options).

**Next build (in testing)**
• Two enlisted crashes: returning to the map after helping take a town, and a CTD when any battle elsewhere ended while you were joining yours.
• The kingdom vote window that could never be closed when one vote followed another.
• 70 of 72 towns started food-negative and starved their own garrisons. Towns now feed themselves.
• Bandit warbands of 200 against caravans of 20 to 36 left caravans parked forever. Both are sized against each other now.
• Free Peoples lords no longer marry orcs. Boromir had married a Misty Mountain orc.
• Executing evil lords no longer tanks your relations with allied Free Peoples factions.
• Troop Weight: the toggle now actually turns it off, and heavy troops take more party space instead of shrinking your party limit.
• Special resources: upkeep shows in the tooltip, every spend gives a message, zero-upkeep troops no longer desert, Black Númenórean upkeep cut to Mordor's line. This was the "wiped after every battle" report.
• Mod Options: 166 settings falsely demanded a restart, and Cancel on that prompt threw your change away. Bandit Scaling settings now apply.
• "Pre-compile Shaders" is back on the main menu. The 5 minute freeze loading big battles was cold shader compilation.
• Hideout boss fight is the boss plus a few bodyguards (default 4, adjustable), not boss plus 20 to 90.
• Fief grants: you no longer win towns from sieges you never joined. Siege participation counts.
• 62 troop upgrades lowered your armour. Fixed; Dale's armour ladder regenerated, Dol Guldur Uruks restated a tier up.
• Ironpass ram cavalry is now recruitable and the line runs four tiers further. Ram herders got their saddles back.
• Battlefield Promotions: a promoted companion can be sent back to the ranks.
• Founding a refuge no longer leaves you in a dead camp menu. Stuck on the current build? Console (Alt+~): `taom.rescue_time`.
• Play as a Lord: "Return to Army" no longer strands you inside a town when the army is not there.
• Nine new Isengard villages (Orthanc had one). New campaign only.
• Also fixed: a save naming removed troops failing to load · dwarf battle voices every 2 to 4 seconds · townswomen with men's bodies in every culture but Rohan · arena practice fighters as toddlers · javelin skirmishers tagged as archers · enlisted player stranded after a siege.

**Armoury and map**
• Gondor troops re-equipped with regional swords, spears, axes and new shields; Rhûn Loke-Rim and Dragon elites moved to heavy plate (Erkam). Lands with the next Armoury sync.
• New Arnor/Númenórean poleaxes, Black Númenórean horse bardings, Pelargir spears redone, shield sizing corrected (Erkam, Solus).
• Helm's Deep wall assets updated; Mirkwood and Mordor scene kits in progress (Solus).
• The campaign map vista is back (it drew white or checkerboard when zoomed out).

**Note: this list is not exhaustive.**

---

## Sources (not for Discord)

Written 2026-09-12 from `git log --since=2026-08-29` on this repo and on `lotraom-assets`, the
CHANGELOG entries for the window, the live `TAOM_Map` folder, and the GitHub issues closed or
opened since 29 Aug. Items already announced in `2026-08-monthly-discord.md` or `v2.0.25-discord.md`
are left out. "Next build" means the fix is on trunk with no in-game smoke yet.

| Bullet | Issue / commit | Bucket |
|---|---|---|
| Enlisted weapons | #525, `b7a3f593` | v2.0.24 |
| AI party size neutral, 193 templates, six Mordor troops | `cd5a6f5e`, `151b6f56`, `bb01b9a4` | v2.0.26 to v2.0.28 |
| Phantom career | #535, `f4273639` | v2.0.27 |
| Ability key rebind | #533, `ad5680d5` | v2.0.27 |
| Skip backstory, tooltip kill | #536 `4c710990`; tooltip fix in `39c237ef` | v2.0.27 |
| Umbar rags | `3f68fd03` | v2.0.26 |
| Two-hander shields, elf arrows | #531 (partial), #529, `59e889ad` | v2.0.24 |
| Party-screen hover CTD | #537, `39c237ef` | v2.0.27 |
| Dead mounts fade | `39c237ef` | v2.0.27 |
| Enlisted crashes | #557, #551, #552, `1e561a95` | next build |
| Kingdom vote | #547, `1707f52e` | next build |
| Settlement food | #546 | next build |
| Bandits vs caravans | #543, #544 (#549 closed) | next build |
| Marriage | #542 | next build |
| Execution relations | #556 | next build |
| Troop weight | #545, #553 | next build |
| Special resources | #558, `9e78afd2` `8bd8917e` `e80949b7` | next build |
| MCM restart flags | #559, `2652fe2f` | next build |
| Pre-compile shaders | #560, #539, `1e654021` | next build |
| Boss fight | #564, `140bad85` `53f39114` | next build |
| Fief grants | #565, `174893ba` | next build |
| Armour ladder | #541, `b8ef3800` | next build |
| War rams, ram saddles | `b54f845c`; CHANGELOG 2026-09-04 and 2026-09-06 | next build |
| Dismiss companion | #540 | next build |
| Refuge dead menu | #567, `4d2ea82b` | next build |
| Return to Army | #566, uncommitted at time of writing | next build |
| Isengard villages | #562; live `settlements.xml` and distance cache rebuilt 12 Sep | next build |
| "Also fixed" line | save-load repair, #548, townsfolk bodies, arena, #554, #538 | next build |
| Gondor re-equip, Rhûn plate | lotraom-assets `d7d5f75b` (Erkam, 12 Sep) | Armoury |
| Poleaxes, bardings, Pelargir, shields | lotraom-assets `0244af88`, `3729340a`, `24ccacee`, `71dcc5b2` | Armoury |
| Helm's Deep, Mirkwood/Mordor kits | live `TAOM_Map` 2 Sep tpacs; lotraom-assets `2acb0a22` and the 11 Sep kit update | Map |
| Vista | CHANGELOG 2026-09-04 `docs(map)` | Map |
