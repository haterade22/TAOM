# Troop Skill Rebalancing — Discord Post

## Short Version (for #announcements or #dev-updates)

---

**Troop Skill Rebalance — 545 Troops Across All 15 Cultures**

We just pushed a massive skill rebalancing pass across every troop tree in TAOM. Here's what changed:

**The Problem:**
Troop skills were all over the place. Some cultures had placeholder values (Rhun infantry all stuck at 150), elves were 3x stronger than intended at certain tiers, cavalry in Umbar and Dunland was half as effective as other factions, and all 40+ militia troops had literally zero combat skills.

**The Fix:**
Every troop now follows a consistent formula: **baseline skills per level + cultural modifiers**. This means:

- **Same-tier troops are within 5-10 skill points of each other** across cultures
- **Each culture has distinct strengths and weaknesses** that reflect their lore identity
- **Elven factions** (Rivendell, Mirkwood, Lothlorien) are elite — 25-50 points above standard factions per skill
- **Militia troops** now actually have combat skills (matching their culture's level 21 counterparts)

**Cultural Highlights:**

| | Best At | Worst At |
|--|---------|----------|
| **Erebor** | Axes, throwing axes, melee | Mounted combat, archery |
| **Iron Hills** | Melee, polearms, crossbows | Mounted combat, archery |
| **Gondor** | Balanced — slight edge in polearms & swords | Throwing weapons |
| **Rohan** | Riding, lance/spear combat | Crossbows |
| **Isengard** | Brute force — athletics, melee | Archery, mounted |
| **Mordor** | Throwing weapons, swords | Athletics, riding, bows |
| **Harad** | Cavalry, archery | Athletics |
| **Rhun** | Polearms, two-handed | Archery, throwing |
| **Dunland** | Throwing weapons, two-handed | Riding |
| **Dol Guldur** | Warg riders | Athletics, polearms |
| **Gundabad** | Heavy brutes — athletics, two-handed | Riding, bows |
| **Umbar** | Swords, polearms | Riding, bows |
| **Rivendell** | Everything (+45-50 all combat) | Elite High Elves |
| **Mirkwood** | Archery (+40), agility (+30) | Elite Wood Elves |
| **Lothlorien** | Balanced elite — bow & melee (+35) | Elite Golden Wood |

**Scale:** 545 troops rebalanced across 13 XML files. Skills scale from level 1 through level 51 across 4 troop groups (Infantry, Ranged, Cavalry, Horse Archer).

The rebalancing tool (`tools/rebalance_troops.py`) is reusable — if we need to adjust baselines or cultural modifiers in the future, it's a config change and re-run.

---

## Long Version (for #dev-log or a pinned post)

---

**Deep Dive: Troop Skill Rebalancing System**

We built a comprehensive troop skill rebalancing system for TAOM that touches 545 troops across all 15 cultures. Here's the technical breakdown for anyone interested.

**How It Works:**

Every troop's 8 combat skills (Athletics, Riding, OneHanded, TwoHanded, Polearm, Bow, Crossbow, Throwing) are now calculated from a formula:

```
Final Skill = Baseline[level][group][skill] + Cultural Modifier[culture][skill]
```

**Baselines** are defined per level tier (1, 6, 11, 16, 21, 26, 31, 36, 41, 46, 51) and per troop group (Infantry, Ranged, Cavalry, Horse Archer). For example, a level 21 infantry troop has a baseline of ~125 OneHanded, ~130 Polearm, ~95 Athletics. A level 21 cavalry troop has ~120 Riding, ~145 Polearm, ~115 OneHanded.

**Cultural Modifiers** are ±5-10 per skill for standard factions, and +25-50 for elven factions. These give each culture its identity:

- **Dwarves (Erebor, Iron Hills):** Strong melee fighters. Erebor favors axes and throwing axes; Iron Hills favors polearms and crossbows. Both suffer on horseback.
- **Men of the West (Gondor):** Disciplined and balanced. Slight edge in swords and polearms, but no dramatic strengths or weaknesses.
- **Rohirrim (Rohan):** Masters of cavalry. +10 Riding and Polearm reflects their lance-charging horse culture. Weakest with crossbows.
- **Uruk-hai (Isengard):** Bred for war. +10 OneHanded and TwoHanded plus +5 Athletics makes them fearsome foot soldiers. Poor archers and riders.
- **Orcs (Mordor):** Strength in numbers, not skill. +5 OneHanded and +10 Throwing, but penalties to Athletics, Riding, and Bow. Slightly below average overall — they win through volume.
- **Haradrim (Harad):** Desert cavalry and archers. +10 Riding and +10 Bow makes their Mumakil riders and archers dangerous. Slightly less athletic on foot.
- **Easterlings (Rhun):** Disciplined polearm formations. +10 Polearm and +5 TwoHanded but weaker ranged capability.
- **Dunlendings (Dunland):** Wild raiders. +10 Throwing and +5 TwoHanded — they hurl javelins and charge in with axes. Can't ride well.
- **Orcs of Dol Guldur:** Warg-riding specialists (+5 Riding for mounted units), but less athletic and weaker with polearms than other orc factions.
- **Gundabad Orcs:** The biggest, meanest orcs. +10 Athletics and +10 TwoHanded — these are the bruisers. Terrible archers and riders.
- **Corsairs (Umbar):** Pirate infantry. +5 OneHanded and Polearm for boarding combat. Poor cavalry and archers.

**Elven Elite Factions:**
- **Rivendell (High Elves):** The best warriors in Middle-earth. +45-50 across all combat skills. A level 21 Rivendell swordsman has ~175 OneHanded vs ~125 for Gondor.
- **Mirkwood (Wood Elves):** Supreme archers. +40 Bow, +30 Athletics, +25 melee. Deadliest ranged troops in the game.
- **Lothlorien (Golden Wood):** Balanced elite. +35 Bow and melee, +30 Athletics. Jack-of-all-trades superiority.

**Weapon Specialization:**
Troops with specific weapon types in their names get skill swaps. A "Crossbowman" swaps Bow↔Crossbow values. A "Pikeman" gets Polearm as primary. A "Swordsman" gets OneHanded boosted. This is detected automatically from troop names.

**Militia Fix:**
Previously, 40+ militia troops (Militia Spearman, Militia Archer, etc.) across all cultures had zero in every skill — they couldn't fight at all. Now they inherit their culture's level 21 baseline, making them functional garrison troops.

**The Tool:**
`tools/rebalance_troops.py` reads all 13 troop XML files, applies the formula, and writes the results back using regex-based replacement (preserving XML formatting, comments, and all non-skill attributes). It supports `--dry-run` mode for previewing changes and `--apply` mode for writing them.
