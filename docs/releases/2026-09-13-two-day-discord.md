**TAOM changelog: 12 to 13 Sep**
Everything here is on trunk and in testing, not in a release yet. Nothing repeats the 29 Aug to 12 Sep post.

**Fixed**
• Mid-battle freezes around wargs and spiders. A dead creature's engine slot was handed to the next horse spawned, so the game asked a horse to play the warg bite. Creature AI also now runs on the main thread, not the engine's async tick. (#592, #595)
• Enlisted: you no longer get the Order of Battle screen or a captaincy (that formation then took no orders), and the party is no longer yanked out of a live battle at high fast-forward. (#576, #577)
• The map bar Extra Fast Forward button now applies the multiplier. Only the E key did before. (#574)
• Smart Cavalry AI works now: forms a line, charges, rides through and comes again. A second F3 charges at once. Still off by default. (#586)
• Black Númenóreans no longer spawn in every Mordor lord's party, only the two houses, Sauron and the vassal reward. (#584)
• 238 empty equipment slots left by the 1 Sep Gondor sword rebuild are repointed, using Erkam's Armoury fix. (#568)
• Veteran militia now stand 15 skill points above basic militia. (#588)
• Four Gondor fiefs recruited the wrong vale. A castle with a household line now offers only that line, towns run 60/40 instead of 80/20. This was the "13 Methir archers after a whole campaign" report. (#598)

**New**
• Wanderers refuse to serve a player of the opposite alignment. No more Aragorn in Mordor's service. (#575)
• Settlement nameplates show friend and enemy again and are translucent like vanilla, with four Mod Options sliders for colour and opacity. (#591, #596)
• Supply Lines: a search box on the order screen finds who stocks a good across every market, nearest first. (#587)
• The encyclopedia troop tree badges troops that cost a special resource. Hover for the costs. (#590)
• Named lords field their own party: Faramir raises Ithilien rangers, Sauron a Black Númenórean and Uruk host. (#580)
• Starting gear: every player-start item is now a weaker starter version (the starter swords sat near the top of the game's damage table), and the six cultures that started in Calradian gear start in their own. (#569)
• Serelond, Methir and Framsburg have villages now (3, 3 and 2). New campaign only. (#597)
• Level 41+ elites weigh 3.0 in Troop Weight instead of 2.0. (#585)

**Balance**
• Every kingdom's armour rebuilt on one curve: one chest cap per kingdom at the elite band (Erebor 70, Rivendell 68, Gondor 57, down to Rohan 40) and every slot and tier follows it. 2,510 items restated. The Gondor tier 9 under Dunland tier 5 report was true for helmets. (#581, #583)
• Archer range now climbs with tier and kingdom rank: 130 generated bows, 227 troops re-bowed, 1,741 inversions to 0. Mirkwood reaches farthest, Dunland shortest. (#582)

**Translations**
• The whole backlog cleared in all 12 languages: camps, career keybinds, wanderer refusals, resource names, the ladder bows. Polish is at parity in the map and Armoury. (#579, #534, #508, #498, #446)

**Performance (lands with the next asset pack)**
• Every Armoury texture downsized in place: 2,474 files, 19 GB to 7.3 GB. Map textures: terrain 1 GB to 157 MB, map icons 1.9 GB to 994 MB, scenes 11 GB to 6.9 GB.
• LODs for the 28 Armoury meshes that had none (Harad body set, Khamûl, Legolas, Rohan horse armour, Sauron's helmet, the mûmak).

**Note: this list is not exhaustive.**

---

## Sources (not for Discord)

Written 2026-09-13 from the commits of 12 and 13 September, the CHANGELOG entries for both days,
the live `TAOM_Map` and `LOTRLOME_Armory` folders, `lotraom-assets`, and the GitHub issues closed or
opened since 12 September. The four items the fortnight post already carried (#565, #566, #567, #562)
are left out, as are tooling and diagnostics entries a player never sees.

| Bullet | Issue / commit |
|---|---|
| Creature freezes | #592, #595, `0c323437` |
| Enlisted OoB screen, battle park | #576 `86f47526`, #577 `fe4cd4a6` |
| Extra Fast Forward button | #574, `a00713b5` |
| Smart Cavalry AI | #586 (closed), `8206a62d` |
| Black Numenorean confinement | #584 (closed), `1ede207b` |
| 238 Gondor refs | #568, `782534fb` |
| Veteran militia | #588 (closed), `d5e43caf` |
| Gondor recruitment vales | #598, `84faba01` |
| Wanderer allegiance | #575, `c6896322` |
| Nameplates | #591 `3a289751`, #596 `fe266439` `c8f72bf1` |
| Supply search | #587 (closed), `8b457cb6` |
| Resource badge | #590 (closed), `fc07d925` |
| Per-hero party templates | #580, `6bf93e51` |
| Starter kit | #569, `feae9143` `49858589` `537465d6` `55baaed5` |
| Serelond, Methir, Framsburg villages | #597, `84faba01`; live `settlements.xml` and distance cache 13 Sep |
| Troop weight 3.0 | #585 (closed), `661a1012` |
| Kingdom armour curve | #581, #583 (closed), `e4de8a78` `c0efbbae` `4bc5e859`; lotraom-assets `3cfa8032` |
| Ranged ladders | #582, `d20838e4` `f46abb62` `1083a8b0` `7b105a8a`; lotraom-assets `229e14d9` |
| Translations | #579, #534, #508, #498, #446 (closed), `df0fa43f` |
| Texture downsizing | CHANGELOG 2026-09-13 `chore(armory-assets)` and `chore(map-assets)`; live folders, uncommitted in lotraom-assets |
| LODs | CHANGELOG 2026-09-13 `chore(armory-assets)` LOD entry |
