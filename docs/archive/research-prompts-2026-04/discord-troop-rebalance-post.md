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
| **Erebor** | Two-handed (+20), axes, throwing (+10) | Mounted combat (-20) |
| **Iron Hills** | Two-handed (+20), polearms (+20), melee (+15) | Riding (-5) |
| **Gondor** | Swords (+10), balanced combat | Throwing weapons (-10) |
| **Rohan** | Riding (+20), lance combat (+10) | Crossbows (-10), athletics (-5) |
| **Isengard** | Two-handed (+15), polearms (+15), crossbows (+10) | Slightly below on riding |
| **Mordor** | Two-handed (+5), throwing (+5) | Athletics, riding, polearms, bows (-5) |
| **Harad** | Cavalry (+15), archery (+10) | Two-handed (-10), polearms (-5) |
| **Rhun** | Riding (+18), polearms (+15) | Bows (-10), crossbows (-10) |
| **Dunland** | Athletics (+20), throwing (+15) | Riding (-5) |
| **Dol Guldur** | Swords & two-handed (+5) | Riding (-10), bows (-5) |
| **Gundabad** | Two-handed (+10), athletics (+5) | Bows (-10), crossbows (-10) |
| **Umbar** | Athletics (+10), swords (+10), two-handed (+5) | Riding (-15) |
| **Rivendell** | Everything (+30-40 all combat) | Elite High Elves |
| **Mirkwood** | Archery (+50), athletics (+45), melee (+40) | Elite Wood Elves |
| **Lothlorien** | Balanced elite — bow (+35), melee & athletics (+30-35) | Elite Golden Wood |

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

- **Dwarves (Erebor, Iron Hills):** Strong melee fighters. Erebor favors two-handed weapons (+20) and throwing axes (+10); Iron Hills favors polearms (+20) and crossbows (+5). Both have excellent athletics (+10) but Erebor especially suffers on horseback (-20).
- **Men of the West (Gondor):** Disciplined and balanced. +10 OneHanded with +5 across Athletics, Riding, TwoHanded, and Polearm. Only weakness is throwing weapons (-10).
- **Rohirrim (Rohan):** Masters of cavalry. +20 Riding and +10 Polearm reflects their lance-charging horse culture. Weakest with crossbows (-10) and slightly less athletic on foot (-5).
- **Uruk-hai (Isengard):** Bred for war. +15 TwoHanded and Polearm, +10 Athletics and OneHanded, plus +10 Crossbow makes them fearsome foot soldiers. Decent at everything.
- **Orcs (Mordor):** Strength in numbers, not skill. +5 TwoHanded and Throwing, but penalties to Athletics, Riding, Polearm, Bow, and Crossbow (-5 each). Below average overall — they win through volume.
- **Haradrim (Harad):** Desert cavalry and archers. +15 Riding and +10 Bow makes their Mumakil riders and archers dangerous. Weaker with two-handed weapons (-10) and polearms (-5).
- **Easterlings (Rhun):** Disciplined polearm formations with strong cavalry. +18 Riding, +15 Polearm, +5 Athletics but weaker ranged capability (Bow -10, Crossbow -10, Throwing -5).
- **Dunlendings (Dunland):** Wild raiders. +20 Athletics and +15 Throwing — they hurl javelins and charge in with axes (+5 TwoHanded). Can't ride well (-5).
- **Orcs of Dol Guldur:** Sword-and-shield fighters (+5 OneHanded, +5 TwoHanded), but poor riders (-10) and weaker archers (-5 Bow, -5 Crossbow).
- **Gundabad Orcs:** The biggest, meanest orcs. +10 TwoHanded and +5 Athletics — these are the bruisers. Terrible archers (Bow -10, Crossbow -10).
- **Corsairs (Umbar):** Pirate infantry. +10 Athletics and OneHanded, +5 TwoHanded for boarding combat. Terrible horsemen (-15 Riding) — sailors through and through.

**Elven Elite Factions:**
- **Rivendell (High Elves):** The best warriors in Middle-earth. +30-40 across all combat skills (+40 TwoHanded/Polearm/Bow/Crossbow/Throwing, +35 Athletics/OneHanded, +30 Riding). A level 21 Rivendell swordsman has ~160 OneHanded vs ~135 for Gondor.
- **Mirkwood (Wood Elves):** Supreme archers. +50 Bow/Crossbow/Throwing, +45 Athletics, +40 OneHanded, +30 TwoHanded/Polearm. Deadliest ranged troops in the game.
- **Lothlorien (Golden Wood):** Balanced elite. +35 Bow/Crossbow/Throwing/Athletics, +30 OneHanded/Polearm, +25 Riding/TwoHanded. Jack-of-all-trades superiority.

**Weapon Specialization:**
Troops with specific weapon types in their names get skill swaps. A "Crossbowman" swaps Bow↔Crossbow values. A "Pikeman" gets Polearm as primary. A "Swordsman" gets OneHanded boosted. This is detected automatically from troop names.

**Militia Fix:**
Previously, 40+ militia troops (Militia Spearman, Militia Archer, etc.) across all cultures had zero in every skill — they couldn't fight at all. Now they inherit their culture's level 21 baseline, making them functional garrison troops.

**The Tool:**
`tools/rebalance_troops.py` reads all 13 troop XML files, applies the formula, and writes the results back using regex-based replacement (preserving XML formatting, comments, and all non-skill attributes). It supports `--dry-run` mode for previewing changes and `--apply` mode for writing them.
