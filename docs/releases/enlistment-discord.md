# :shield: Enlist — take a lord's coin and serve in the ranks

Every campaign starts the same way: you are nobody, and the only ladder out is *lead your own warband*. Recruit peasants, lose them, recruit more. There has never been a way to simply **soldier** — to swear to a lord, march where he marches, and earn your way up from the back rank. **Enlistment** is that missing path. Find a lord in the field, swear the oath, and you serve in his column as a common soldier until your term is up.

## :dart: What it does

**1. Swear the oath to any lord in the field.** Talk to him and pick *"I wish to enlist under your command."* He asks for it properly — *"your blade at my side, my bread in your pack, until your term is served"* — and you answer *"I swear it."* Your term runs **365 days**. You can't enlist while you hold a mercenary contract, and no lord will take you while his faction is at war with your own kingdom.

**2. Your party marches with you, out of sight.** Your own troops and companions don't disband — they're parked with the column and follow your commander wherever he goes. The map hands you a **service wait menu** instead of your usual free roam: *"You serve in Théoden's company. The column moves at your commander's pace."* When he fights, you're pulled into the battle on his side.

**3. You get paid out of his purse — not thin air.** Wages are **5 / 8 / 14 / 22** a day by rank. He pays from his own gold, and if his coffers run low the pay **defers into arrears** (capped at 60) instead of vanishing. When he's flush again, he settles up. Serve out your term and the paymaster clears what's owed on the way out — **desert and you forfeit every copper of it.**

**4. Earn your stripes: Recruit → Soldier → Veteran → Sergeant.** Promotion is checked at the day's end *and* right after a battle, so the fight that earns your last XP promotes you on the spot rather than tomorrow morning.
- **Soldier** — 7 days, 100 service XP
- **Veteran** — 25 days, 350 XP, Leadership 20, 2 duties done
- **Sergeant** — 60 days, 800 XP, Leadership 50, 5 duties, trust 6

**5. Take a place in the line, and change it later.** **Infantry, Archer, Cavalry** or **Support** — *"I can ride. Give me a horse."* Your assignment routes your daily training and decides which duties you're offered. Switching costs a **7-day cooldown and a point of your commander's trust**, so pick with intent.

**6. Fight well, not just often.** Every battle is scored **0–100** on how you actually fought: kills (capped, so farming stragglers won't carry you), whether you survived, whether you held formation near your captain, whether you stayed in contact, and whether you fought **the way your assignment asks** — archers holding an 18–50m shooting line, cavalry working the flanks, support staying near the commander, infantry holding the line *and* trading blows. The sergeants notice: *"The sergeants noted your conduct: distinguished."*

**7. 27 pieces of camp and campaign life.** **13 field duties** — hunt a band of deserters, carry a dispatch to an ally, run supplies, forage, stand a watch — plus **11 in-camp duties** resolved on a skill check (night patrol, treat the wounded, drill the recruits, gate watch, the quartermaster's shortage — and four officer tasks that open up once you're a **Veteran** your commander trusts, the last of them **Sergeant**-only), and **3 camp incidents** where the pay is late or the rations are short and you decide how to handle it. What you're offered depends on what the column is actually doing — under siege, at sea, marching, or sitting in garrison.

**8. Draw your kit from the quartermaster.** *"I would draw my service kit from the quartermaster."* — once per rank, and it's **your faction's** kit, not Calradian hand-me-downs. **64 authored armour sets** — 16 cultures at four ranks each, plus a neutral fallback set — seeded from each culture's own troop tree, so a dwarf column issues dwarf armour and an orc host issues orc armour. Ask twice and you'll be told: *"You have drawn your due for your rank already. Earn the next stripe first."*

**9. Your service actually counts.** Fighting inside another lord's army used to make you invisible to half the game's bookkeeping. Now **sieges you help storm advance the War of the Ring**, and lords your column captures credit your career quests — you get the credit for the war you're actually fighting.

**10. Leaving, honourably or otherwise.** Serve your 365 days and you're told *"Your term of service is complete — you may ask for release with honor, or march on."* Ask before that and it's **desertion**: you forfeit your arrears. Your commander can also release you — *"You have marched well enough. Go, then — you are released."* If he dies you're free; if he's captured you get a **7-day grace period** to roam before service formally ends, rather than being cut loose the instant he's taken.

*(Whatever happens, the moment service ends your party is handed straight back to you — visible, active, and yours.)*

---

# :crossed_swords: Battlefield Promotions — the soldier who earned a name

The Erebor axeman who has carried your line through six battles is, mechanically, identical to the one you recruited yesterday. **Battlefield Promotions** fixes that: a troop who proves themselves in a fair fight can be raised into a **named companion**.

## :dart: What it does

**1. Merit is earned in fair fights only.** Kills are tracked **per troop type** in battles you win where you weren't massively outnumbering the enemy (ratio threshold **1.3**). One kill, one point of merit; **8 points** and that troop type is ready.

**2. You're offered the promotion after the fight.** *"{TROOP_NAME} has distinguished themselves in battle. Promote them to a companion?"* Accept and they leave the ranks as a hero — then *"Give {TROOP_NAME} a name, or keep the one they carried into battle."*

**3. Declining costs you nothing.** Merit is only spent when a promotion actually completes. Say *"Not Yet"*, quit to the menu, or have a full retinue and the merit stays banked — you'll be asked again: *"There is no room for another companion right now. {TROOP_NAME}'s promotion will be offered again later."*

**4. They keep who they were.** The new companion carries the gear they fought in and skills budgeted to their level, so a veteran soldier becomes a credible companion rather than an instant superhero with an incoherent character sheet.

**5. Sensible limits.** Only **human, dwarf and elf** troops can be raised (no promoting a cave troll into your clan), and the companion cap is respected rather than ignored. While you're **enlisted**, promotions pause entirely — inside a lord's army the fair-fight test stops meaning anything.

## :tools: Where to edit it

**Wages, ranks, promotion thresholds, merit scoring, duty cadence:**
`Main/_Module/ModuleData/enlistment/enlistment_config.json`

**The duties themselves — all 27 rows:**
`Main/_Module/ModuleData/enlistment/enlistment_duties.json`

**Battlefield promotion tuning:**
`Main/_Module/ModuleData/field_commission/field_commission_config.json`

**Per-rank service armour (68 rosters — 17 sets × 4 ranks):**
`Main/_Module/ModuleData/equipmentsets/taom_enlistment_equipment.xml`

Duties are data rows — flavour, gates, deadline and rewards all live in JSON, and a malformed row is skipped with a warning rather than silently doing something else:

```jsonc
// enlistment_duties.json — a field duty
{
  "id": "bandit_hunt",
  "mechanic": "HuntSpawnedParty",
  "deadlineDays": 4,
  "reportReward": { "serviceXp": 54, "gold": 60, "trust": 2 },
  "gates": { "minRank": "Recruit" }
}
```

```jsonc
// field_commission_config.json
{ "ratioThreshold": 1.3, "meritThreshold": 8, "allowedRaceNames": ["human", "dwarf", "elf"] }
```

To regenerate the service armour after a troop-tree change:
`python tools/generate_enlistment_rosters.py --apply` then `python tools/audit_enlistment_roster_coverage.py`.

Testing promotions without grinding out battles — two dev-console commands:
`taom.fc_grant_merit [troopId] [amount]` and `taom.fc_status`.

> **Caveats, plainly.** Config is cached for the whole process — retuning needs a **full game restart**, not a save-load. The in-camp duty prompts (titles and body text) are still **hardcoded in C#**, so the JSON controls a duty's gates, deadline and rewards but not its wording. Khand and Lothlórien have no per-culture kit yet and draw the neutral set. **Neither feature has MCM sliders yet:** Battlefield Promotions can at least be switched off with `"enabled": false` in its JSON, but **Enlistment currently has no off switch at all** — it's always registered.
