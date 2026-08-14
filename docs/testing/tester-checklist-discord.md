# MESSAGE 1 — Overview & Setup

# :shield: TAOM Tester Checklist — Full System Testing

Welcome testers! Work through each section below and report any **crashes, visual bugs, missing equipment (underwear characters), or incorrect behavior**.

## :wrench: Pre-Test Setup
- Ensure **LOTRLOME_Armory** module is installed (NOT `Armory_2`)
- Ensure **Alliance.Wargs** module is installed
- Start with a **fresh new campaign** for most tests
- Enable **MCM** mod settings menu if available

---

# MESSAGE 2 — Character Creation

## 1. :bust_in_silhouette: CHARACTER CREATION (16 cultures)

**1a. Culture/Race Selection**
- Start new campaign — faction map UI loads with all regions clickable
- All 16 cultures selectable: Gondor, Mordor, Erebor, Rivendell, Lothlorien, Mirkwood, Isengard, Gundabad, Dol Guldur, Umbar, Harad, Dunland, Easterlings, Dale, Rohan, Barding
- Faction colors, descriptions, and lore text display correctly
- Selecting a culture shows correct race model (elf for Rivendell, dwarf for Erebor, orc/uruk for evil factions)
- Race dropdown in FaceGen shows all races without broken indices

**1b. Narrative Stages (Parents > Childhood > Youth)**
- **Parents stage:** 6 options per culture with LOTR lore text
- Each parent option grants different skill/attribute bonuses — check tooltips
- **Childhood stage:** 6 universal options for all cultures
- **Youth stage:** 5-6 culture-specific options with career paths
- Youth equipment changes based on selected career (not all identical)

**1c. Equipment & Appearance**
- Character is NOT in underwear at any creation stage
- Final character has culture-appropriate starting gear
- Non-human races display correct body model (dwarf shorter, elf taller, orc hunched)
- Female characters have correct animations
- Dwarf portrait shows correct vertical offset (not clipped)

**1d. Finalization**
- Player spawns at correct starting settlement for their culture
- Player race is set correctly (check encyclopedia)
- Starting age matches culture defaults

**Test each group at least once:**
:green_circle: Good Human — Gondor, Rohan, Dale
:green_circle: Elven — Rivendell, Lothlorien, Mirkwood
:green_circle: Dwarven — Erebor
:red_circle: Evil Human — Umbar, Harad, Dunland, Easterlings
:red_circle: Orc/Uruk — Mordor, Isengard, Gundabad, Dol Guldur

---

# MESSAGE 3 — Race Age & Offspring

## 2. :hourglass: RACE AGE SYSTEM

**2a. Lifespan & Aging** (use cheats/fast-forward to verify)
- Elves: effectively immortal (~10,000 year max)
- Dwarves: die of old age around ~250 years
- Humans: die of old age around ~200 years
- Orcs: die young, max ~60 years
- Nazgul/Saruman: immortal — never die of old age
- Cave Trolls: max ~500 years

**2b. Fertility & Pregnancy**
- Human fertility: ages 18-45
- Dwarf fertility: ages 30-120, slower rate (0.6x)
- Orc/Uruk: faster breeding (2x modifier)
- Evil cultures (Mordor, Isengard, Gundabad, DG): NO initial children at game start

**2c. Offspring Race Inheritance**
- Male children inherit father's race
- Female children inherit mother's race
- Cross-race couples: no crash

---

# MESSAGE 4 — Diplomacy & War of the Ring

## 3. :crossed_swords: DIPLOMACY & WAR OF THE RING

**3a. Permanent Alliances**
- Rivendell-Lothlorien-Mirkwood permanently allied from game start
- Free Peoples alliances cannot be broken through diplomacy
- Attempting to end a permanent alliance via kingdom decisions = blocked

**3b. War of the Ring Phases**
- **Phase 1** (early game): Isengard + Dunland declare war on Rohan
- **Phase 2** (shortly after): All hostile pairs from config go to war
- After Phase 2: peace proposals between hostile factions are **blocked**
- Kingdom decision menu does NOT show "Make Peace" for locked pairs
- No crash when AI attempts peace during war lock
- **MCM toggle:** Disable War of the Ring — verify no auto-wars
- **MCM test mode:** Rapid phase transitions for faster testing

**3c. Faction Relationships**
- Evil factions hostile to Free Peoples from the start
- Umbar (neutral) treated as enemy by both sides
- Alliance tiers: Permanent > Allied > Neutral > Hostile

---

# MESSAGE 5 — Execution System

## 4. :axe: EXECUTION SYSTEM (Alignment-Aware)

**4a. Cross-Alignment (Good executes Evil or vice versa)**
- Capture evil lord as Free Peoples > execute > **0 honor penalty**
- Allied faction relation penalty = **0** (no dishonor for killing enemies)
- Execution dialogue appears normally

**4b. Same-Alignment (Kinslaying)**
- Capture good lord as Free Peoples > execute > **1.5x vanilla penalties**
- Relation penalty with own clan should be ~-90 (vs vanilla -60)
- Honor XP penalty applies

**4c. Edge Cases**
- Execute a Neutral (Umbar) lord — treated as enemy by both sides
- No crash when executing any lord from any faction
- Multiple executions in sequence = no errors

---

# MESSAGE 6 — Troop Weight

## 5. :scales: TROOP WEIGHT SYSTEM

**5a. Party Capacity**
- Standard troops (1x weight) — party count normal
- Elite troops (2x weight, e.g. elf commanders, warg riders) — count +2 per unit
- Cave trolls (4x weight) — count +4 per unit
- Legendary commanders (3x) — count +3 per unit
- Party screen shows weighted count, not raw unit count

**5b. UI Accuracy**
- Party management screen shows correct weighted party size
- Recruitment screen shows correct remaining capacity
- No visual glitches in party list labels

**5c. MCM Toggle**
- Disable "Enable Troop Weight" in MCM > all troops count as 1x
- Re-enable > weighted counts resume immediately
- No crash when toggling mid-game

---

# MESSAGE 7 — Warg Combat

## 6. :wolf: WARG COMBAT SYSTEM

**6a. Warg AI in Battle**
- Deploy Dol Guldur, Gundabad, or Isengard warg troops in battle
- Wargs actively attack nearby enemies (not standing idle)
- Warg attacks deal visible damage
- No error spam in logs (check `rgl_log_*.txt`)

**6b. Rage Mode**
- Take >10 damage on a warg mount — 10% chance rage activates
- During rage: warg takes over movement for 2-3 attacks
- After rage: control returns to rider

**6c. Riderless Wargs**
- Kill a warg rider — warg continues fighting on its own
- No crash when warg has no rider
- Riderless wargs pathfind to enemies

**6d. Stress Test**
- 10+ warg riders in a single battle — no performance issues
- Player riding a warg — rage mode interaction with player control

---

# MESSAGE 8 — Startup Resources & Atmosphere

## 7. :moneybag: STARTUP RESOURCES

**Gold Distribution (new campaign).** Flattened 2026-08-14, so most cultures are now identical
- Every culture's lords: 250,000 starting gold
- Except the four elven realms (Rivendell, Lothlorien, Mirkwood, Lindon): 500,000
- No other culture is a special case. Erebor, Umbar, Gondor and the orc kingdoms are all on the flat 250,000
- Player hero gets culture `playerGold` at character creation instead, which was NOT flattened (elves 4,000, Erebor 3,500, everyone else 2,000). Lord gold skips the player clan
- Live values: `Main/_Module/ModuleData/startup_resources/startup_resources_config.xml`

**Influence Distribution**
- Every culture's clans: 1,000 starting influence, with no exceptions
- Check AI clans in encyclopedia to verify

**Idempotency**
- Save + reload immediately after campaign start — resources NOT doubled

---

## 8. :fog: ATMOSPHERE PERSISTENCE

- Enter "forceatmo" scenes (Dead Marshes, Moria) — baked atmosphere preserved, NOT campaign weather
- Exit and re-enter — atmosphere persists
- Regular battle scenes still use campaign weather
- Siege scenes work normally
- No crashes on any scene type

---

# MESSAGE 9 — Weather, Banners, Troops

## 9. :cloud_rain: WEATHER BOUNDS GUARD

- Travel campaign map extensively — no extreme weather visual glitches
- No white-out or zero-visibility events
- Snow/rain don't exceed reasonable intensity

---

## 10. :triangular_flag_on_post: BANNER SYSTEM

- Banner editor allows >32 layers
- Multi-layer faction banners display correctly
- Save > reload > banner preserved
- Banners show on shields, flags, and UI

---

## 11. :shield: TROOP TREES & RECRUITMENT

**Gondor (182 troops)**
- Visit settlements — recruit from correct regional pools (Anorien, Minas Tirith, etc.)
- Upgrade paths work all the way to T9 (Swan Knights, Fountain Guard, Moon Guard)
- No underwear troops

**Erebor (41 troops)**
- Miner > Militia > branches work
- Noble line reaches Royal Warden; Oathsworn elite reachable

**Dol Guldur**
- Goblin: Runt splits to Harrier (melee) + Crawler (ranged)
- Orc Recruit > Gnasher AND Warg Scout (dual path)
- Khamul humans (T4-T9): Shadow Initiate entry, all 14 troops upgradeable

**Rhun (113 troops)**
- All 11 unit groups recruit: Easterling Regular, Loke-Rim, Dragon-Wrath, Wainriders, Black Sun, Darkhun, etc.

**All Other Factions**
- Spot-check Rohan, Rivendell, Mirkwood, Lothlorien, Mordor, Isengard, Gundabad, Umbar, Harad, Dunland
- **No faction should have underwear troops**

---

# MESSAGE 10 — Lords, NPCs, Names

## 12. :crown: LORDS & NPCs

**Lord Equipment**
- Inspect 5-10 lords per faction in encyclopedia — all wearing visible equipment
- Gondor lords use new armor (not old `citidel_guard` items)
- Portraits render with race-appropriate appearance

**Lord Skills**
- Warriors: high combat, low non-combat
- Politicians/managers: high non-combat, low combat
- Legendary lords (Nazgul, Sauron, Witch-King): extremely high stats
- Junior lords: ~60% of senior stats

**Clan Ownership**
- No orphaned clans — every clan has a valid owner
- Harad clans (aserai_10-26) have unique heroes
- Umbar clans (umbar_2-6) have unique heroes

**Notable NPCs**
- Visit towns/villages — merchants, preachers, artisans, gang leaders, headmen present
- No crash interacting with any notable

---

## 13. :speech_balloon: KINGDOM & FACTION NAMES

- No "The Erebor" or "The Mirkwood" in diplomacy messages
- Alliance msgs: "Erebor **has** formed" (not "have formed")
- Lord titles: "Daeron of Mirkwood" (not "of the Mirkwood")

---

# MESSAGE 11 — Siege, UI, Save/Load

## 14. :european_castle: SIEGE SYSTEM

- Start sieges on various settlements — no crash
- Siege camps appear correctly even at settlements with missing scene entities
- Camp visuals not floating or underground

---

## 15. :desktop: UI & VISUAL

- Party screen: PartyListPanel displays correctly, auto-scroll works, upgrade buttons work
- Custom TAOM fonts render in all menus, no garbled text
- Faction map (character creation): region highlights, tooltips, scrolling all work

---

## 16. :floppy_disk: SAVE/LOAD COMPATIBILITY

- Save after 10+ in-game days > reload > no crash
- Custom data persists: race assignments, diplomacy state, troop weights, War of the Ring phase
- Old orphaned Erebor troops don't cause errors
- >32 layer banners survive save/load

---

# MESSAGE 12 — Children, Minor Factions, Performance, Regressions

## 17. :baby: CHILD EQUIPMENT

- Trigger offspring delivery (or cheat) — no crash
- Children of all 10 custom cultures wear culture-appropriate clothing
- Evil culture children: no crash even with initial child gen disabled

---

## 18. :ghost: MINOR FACTION SPAWNING

- New campaign start — no crash at `CharacterObject.get_StealthEquipments()`
- Minor faction heroes (e.g. Ghilman) spawn on map
- Dunland, Harad, Rohan, Rhun cultures have working stealth equipment

---

## 19. :chart_with_upwards_trend: PERFORMANCE & STABILITY

- Campaign map runs without freezing
- Large battles (200+ units) with warg riders — no severe FPS drops
- Loading screen completes without hanging
- Check `rgl_log_*.txt` for repeated errors / log spam
- Extended play (1+ hour) — no memory leaks or progressive slowdown

---

# MESSAGE 13 — Crash Regression & Bug Reporting

## :rotating_light: CRASH REGRESSION CHECKLIST

These were previously fixed — verify they DON'T recur:
1. `NullReferenceException` at `CharacterObject.get_StealthEquipments()` — minor faction spawn
2. `IndexOutOfRangeException` in siege camp positioning — starting a siege
3. `NullReferenceException` in settlement party menu — villager party overlay
4. `NullReferenceException` in kingdom change action — orphaned clan owners
5. `ArgumentException` in BehaviorTrees.dll — warg behavior tree
6. `NullReferenceException` on offspring delivery — child equipment
7. `NullReferenceException` in troop weight calculations — null MCM settings

---

## :bug: HOW TO REPORT BUGS

Please include:
1. **Steps to reproduce** — exact actions taken
2. **Expected result** — what should have happened
3. **Actual result** — what happened (screenshot/video if visual)
4. **Error log** — `%ProgramData%\Mount and Blade II Bannerlord\logs\rgl_log_*.txt`
5. **Culture/faction** — which culture you were playing
6. **Save file** — if reproducible from a save

Thank you for testing! :heart:
