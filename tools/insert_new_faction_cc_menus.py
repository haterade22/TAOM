#!/usr/bin/env python3
"""Clone the gundabad character-creation narrative menu entries for the new orc cultures
(goblin, mistymountainorcs). Without these, the CC Family/Youth/Adulthood/Education stages render
BLANK when the player picks one of the new cultures: NarrativeMenuBuilder filters entries by
`culture_id == selectedCulture.StringId`, and the new cultures had zero entries (RCA 2026-06-02).

Childhood is culture-INDEPENDENT (entries have no culture_id) — shared across all cultures — so it
needs no per-culture entries. parents/youth/adulthood/education are culture-keyed (6 gundabad entries
each).

Inline text/description are plain English, remapped from the source culture's wording (or written
outright in AUTHORED). NarrativeMenuBuilder (NarrativeMenuBuilder.cs:74-75) builds a `taom_cc_<id>_text`
/ `_desc` key per entry and looks it up in the language files, so every entry of a culture in NEW is
registered in taom_cc_strings.xml (prepare_cc_strings, which skips rows already present).

TEXTUAL append (not json.dump) so the existing entries stay byte-for-byte identical (clean diff) and
the file's CRLF / inline-array style is preserved. Idempotent: skips a file that already contains the
new-culture entries (revert + re-run to regenerate).

Run: python tools/insert_new_faction_cc_menus.py            # dry run: what would be added
     python tools/insert_new_faction_cc_menus.py --apply    # write the menus and the strings
     python tools/insert_new_faction_cc_menus.py --check    # exit 1 if anything is still to add
"""
import argparse, json, os, re

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CC = os.path.join(ROOT, "Main", "_Module", "ModuleData", "charactercreation")
MENUS = ["parents", "youth", "adulthood", "education"]
SRC = "gundabad"
NEW = ["goblin", "mistymountainorcs", "bluecraig", "lindon", "arthedain", "cardolan", "rhudaur",
       "eredluin", "anduinvale", "angaladh"]

# Which culture each new one clones its narrative options from. The first two were carved out of
# Gundabad; Blue Craig is carved out of Goblin-town and Lindon out of Rivendell, so they clone the
# cultures they were promoted from rather than the original orc source.
SRC_FOR = {
    "goblin": "gundabad",
    "mistymountainorcs": "gundabad",
    "bluecraig": "goblin",
    "lindon": "rivendell",
    # Arthedain is a new culture, not a promoted one: it takes Gondor's MECHANICS entry for entry
    # (skills, attribute, focus, occupation_type, title_type at the same index) and its own
    # AUTHORED text below, because Gondor's wording is Gondor through and through (Swan Knights,
    # the Pelennor, Osgiliath) and no word swap turns that into the North-kingdom.
    "arthedain": "gondor",
    # Cardolan and Rhudaur (#978) the same way: Gondor's mechanics, their own AUTHORED text.
    "cardolan": "gondor",
    "rhudaur": "gondor",
    # Ered Luin, the Dwarves of the Blue Mountains, takes Erebor's mechanics (the other Dwarf
    # kingdom) with AUTHORED text: Erebor's is the Lonely Mountain, Dale and Dain throughout.
    "eredluin": "erebor",
    # Anduin Vale and Angáladh are cut from Dale (culture id `sturgia`): Dale's mechanics entry for
    # entry, their own AUTHORED text, because Dale's wording is Dale (the Lonely Mountain, Lake-town).
    "anduinvale": "sturgia",
    "angaladh": "sturgia",
}

# Per culture, per menu: (text, description) in the SOURCE culture's entry order. A culture listed
# here takes this text instead of the SUBS remap; the count must equal the source's entry count.
AUTHORED = {
    "arthedain": {
        "parents": [
            ("Noble Houses of Arthedain",
             "Your family belonged to one of the old houses of Arthedain, whose blood runs back to the Númenóreans who came north with Elendil. Your father rode in the King's host at Fornost, while your mother kept the family's thinning lands and its long memory."),
            ("Merchants of Bree",
             "Your family kept a trading house in Bree, where the Great East Road meets the Greenway. Dwarves from the Blue Mountains, Hobbits from the Shire and Men of the Bree-land all passed through their doors, and every one of them paid."),
            ("Farmers of the North Downs",
             "Your family worked the thin soil of the North Downs below Fornost. They sent a son to the levy every spring and kept a spear by the door, for Angmar's raiders came over the hills more often than the rain."),
            ("Smiths of Fornost",
             "Your family were smiths and stonewrights in Fornost Erain, mending the walls the Witch-king's hosts broke and forging the blades that held them. Their craft came down unbroken from Númenórean masters."),
            ("Rangers of the North",
             "Your family were Rangers of the Dúnedain, walking the empty lands of Eriador to keep its roads and borders. Few thanked them and fewer knew their names, but the wild places of the North were safer for their watch."),
            ("Drifters of the Greenway",
             "Your family lived on the roads, drifting between Bree, the borders of the Shire and the ruined towns of fallen Cardolan. They traded what they found, took what they could, and learned that a stranger's purse was often looser than his tongue."),
        ],
        "youth": [
            ("Rode with the King's horse at Fornost.",
             "You joined the mounted companies of Fornost, learning to ride and fight with the lance under knights who still carry the old banners of the North-kingdom."),
            ("Stood watch on the walls of Fornost.",
             "You served in the garrison of Fornost Erain, keeping the night watch on its walls and learning the craft of siege defence from captains who had held them against Angmar."),
            ("Rode with the Rangers of the North.",
             "You followed the Rangers along the Greenway and through the Weather Hills, striking at orcs and wolves from cover and learning to read the wild as other men read letters."),
            ("Trained with the Guard of the Citadel.",
             "You joined the King's guard in the citadel of Fornost, training in disciplined infantry warfare to protect the King and the hall of his fathers."),
            ("Lived by your wits in the ruins of Cardolan.",
             "You scraped by among the broken towns of fallen Cardolan, where barrow-robbers and outlaws made their living. You learned the value of a quick hand, a well-thrown stone and a story nobody could check."),
        ],
        "education": [
            ("Drilled with the sons of the old houses.",
             "You grew up training alongside the children of Arthedain's noble families in the yards of Fornost. You learned sword and lance under knights sworn to the King, and the long lineages of the Dúnedain by heart."),
            ("Apprenticed at the Bree market.",
             "You spent your youth carrying ledgers and running errands in the market of Bree, where every road in Eriador seems to meet. You learned to bargain with Dwarves, flatter Hobbits and never trust a Southerner's scales."),
            ("Labored on the farms of the North Downs.",
             "You worked from dawn to dusk on the cold farms below Fornost, hauling stone and driving sheep across the downs. When the levy was called you were handed a spear and taught to hold a line."),
            ("Apprenticed to a smith of Fornost.",
             "You learned your craft in the forges of Fornost Erain, working the bellows under a master who still kept the Númenórean ways of tempering steel. By the time you were grown your hands knew the work better than your head."),
            ("Ranged the wilds with the Dúnedain.",
             "You followed the Rangers as a youngster, learning to move without sound through the Chetwood and along the Hoarwell. You strung your first bow before you could grow a beard, and your first orc fell to it soon after."),
            ("Scrounged along the Great East Road.",
             "You grew up on the road between Bree and the Brandywine, running messages for coin and selling what travellers left behind. The road taught you quick hands, a quicker tongue and when to vanish."),
        ],
        "adulthood": [
            ("You held the Weather Hills against a host of Angmar.",
             "When the hill-men of Rhudaur came over the Weather Hills under Angmar's banners, you held your line on the slopes until the King's riders came. Your name was spoken in the hall at Fornost."),
            ("You won the Dwarves of the Blue Mountains as partners in trade.",
             "When iron ran short in Fornost's forges, you rode west to the Ered Luin and bargained with the Dwarves until the ore carts rolled east again. The King's treasurer does not forget who kept the forges lit."),
            ("You brought in the harvest of the North Downs under threat of wolves.",
             "When wolves and orcs came down from the north in a hard autumn, you gathered the farmers' levies to guard the fields and brought the harvest in before the snow. The villages remember."),
            ("You forged a blade presented to a knight of Fornost.",
             "A knight of the King's guard had heard of your work and commissioned a sword for the day he took his oath. When he drew it before the assembled guard and it rang true, you knew your name was made."),
            ("You guided a Ranger company out of an ambush in the Ettenmoors.",
             "When a Ranger patrol was cut off in the Ettenmoors, you led them out by a path no orc scout knew, losing not a single man. Their captain clasped your arm and named you a friend of the Dúnedain."),
            ("You uncovered a band of barrow-robbers working out of Bree.",
             "For months grave-goods from the Barrow-downs had been turning up in the back rooms of Bree. You traced the trade, found the men behind it and handed them to the King's wardens, keeping a little of the gold for your trouble."),
        ],
    },
    "cardolan": {
        "parents": [
            ("Noble Houses of Cardolan",
             "Your family belonged to one of the old houses of Cardolan, whose fathers lie in the royal barrows of Tyrn Gorthad. Your father rode in the King's host from Caras Gorthad, while your mother kept the family's lands along the Greenway and its long memory."),
            ("Merchants of Tharbad",
             "Your family kept a trading house in Tharbad, where the North-South Road crosses the Greyflood. Barges from the coast, wool from the downs and Dwarvish iron from the west all passed through their warehouses, and every bale paid its toll."),
            ("Farmers of Minhiriath",
             "Your family worked the wide fields of Minhiriath between the rivers. They sent a son to the levy every spring and kept a spear by the door, for orcs and outlaws came up the Greyflood more often than the floods."),
            ("Smiths of Caras Gorthad",
             "Your family were smiths and wrights in Caras Gorthad, shoeing the horses of the King's riders and forging the spearheads Cardolan is known for. Their craft came down unbroken from Númenórean masters."),
            ("Wardens of the Barrows",
             "Your family were wardens of Tyrn Gorthad, keeping watch over the tombs of the kings and the downs around them. Few honoured them and fewer knew their names, but no robber walked the barrows while they kept the watch."),
            ("Drifters of the Greenway",
             "Your family lived on the roads, drifting between Tharbad, Sarn Ford and the edge of the Old Forest. They traded what they found, took what they could, and learned that a stranger's purse was often looser than his tongue."),
        ],
        "youth": [
            ("Rode with the King's riders of Cardolan.",
             "You joined the mounted companies of Caras Gorthad, the best horsemen of the three northern kingdoms, learning to ride and fight with the lance across the open plains."),
            ("Stood watch on the walls of Caras Gorthad.",
             "You served in the garrison of Caras Gorthad, keeping the night watch on its walls and learning the craft of siege defence from captains who had held them against the wild men of the south."),
            ("Hunted along the Greyflood with the Minhiriath bowmen.",
             "You followed the bowmen of Minhiriath along the Greyflood and the Swanfleet marshes, striking at orcs and outlaws from cover and learning to read the river country as other men read letters."),
            ("Trained with the King's spearmen.",
             "You joined the spear companies of the King's guard, training in disciplined infantry warfare to hold a line of Cardolan spears against horse and foot alike."),
            ("Lived by your wits on the wharves of Tharbad.",
             "You scraped by on the wharves of Tharbad, where smugglers and cutpurses made their living among the barges. You learned the value of a quick hand, a well-thrown stone and a story nobody could check."),
        ],
        "education": [
            ("Drilled with the sons of the old houses.",
             "You grew up training alongside the children of Cardolan's noble families in the yards of Caras Gorthad. You learned sword and lance under knights sworn to the King, and the lineages of the barrow-kings by heart."),
            ("Apprenticed at the Tharbad market.",
             "You spent your youth carrying ledgers and running errands in the market of Tharbad, where the roads and the river meet. You learned to bargain with bargemen, flatter Dwarves and never trust a Dunlending's scales."),
            ("Labored on the farms of Minhiriath.",
             "You worked from dawn to dusk on the farms between the rivers, hauling grain and driving cattle to the fords. When the levy was called you were handed a spear and taught to hold a line."),
            ("Apprenticed to a smith of Caras Gorthad.",
             "You learned your craft in the forges of Caras Gorthad, working the bellows under a master who still kept the Númenórean ways of tempering steel. By the time you were grown your hands knew the work better than your head."),
            ("Walked the downs with the barrow-wardens.",
             "You followed the wardens of Tyrn Gorthad as a youngster, learning to move without sound across the downs and through the eaves of the Old Forest. You strung your first bow before you could grow a beard."),
            ("Scrounged along the North-South Road.",
             "You grew up on the road between Tharbad and Sarn Ford, running messages for coin and selling what travellers left behind. The road taught you quick hands, a quicker tongue and when to vanish."),
        ],
        "adulthood": [
            ("You held Sarn Ford against a host from Dunland.",
             "When the Dunlendings came north over the Greyflood in force, you held your line at Sarn Ford until the King's riders came. Your name was spoken in the hall at Caras Gorthad."),
            ("You won the Dwarves of the Blue Mountains as partners in trade.",
             "When iron ran short in the forges of Caras Gorthad, you rode west to the Ered Luin and bargained with the Dwarves until the ore carts rolled east again. The King's treasurer does not forget who kept the forges lit."),
            ("You brought in the harvest of Minhiriath before the floods.",
             "When the rivers rose early in a hard autumn, you gathered the farmers' levies to bring the harvest in and raised the dykes before the Greyflood broke them. The villages remember."),
            ("You forged a spear presented to a knight of Cardolan.",
             "A knight of the King's riders had heard of your work and commissioned a lance for the day he took his oath. When he couched it before the assembled companies and it ran true, you knew your name was made."),
            ("You guided a warden patrol out of an ambush in the Old Forest.",
             "When a patrol of barrow-wardens was cut off under the eaves of the Old Forest, you led them out by a path no outlaw knew, losing not a single man. Their captain clasped your arm and named you a friend of the Dúnedain."),
            ("You uncovered a band of barrow-robbers working out of Tharbad.",
             "For months grave-goods from Tyrn Gorthad had been turning up in the back rooms of Tharbad. You traced the trade, found the men behind it and handed them to the King's wardens, keeping a little of the gold for your trouble."),
        ],
    },
    "rhudaur": {
        "parents": [
            ("Noble Houses of the Angle",
             "Your family belonged to one of the Dúnedain houses of the Angle, between the Hoarwell and the Loudwater, whose blood runs back to Eärendur's youngest son. Your father rode in the King's host from Cameth Brin, while your mother kept the family's hill lands and its long memory."),
            ("Traders of the Last Bridge",
             "Your family kept a trading post at the Last Bridge, where the Great East Road crosses the Hoarwell. Dwarves on the road to the Iron Hills, hill-men with furs and Elves from the hidden valley all passed their doors, and every one of them paid."),
            ("Herders of the Rhudaur fells",
             "Your family were hill-men who herded sheep and goats on the fells of Rhudaur. They sent a son to the levy every spring and kept an axe by the door, for trolls and orcs came down from the mountains more often than the snow."),
            ("Smiths of Cameth Brin",
             "Your family were smiths in the tower town of Cameth Brin, forging the axes of the hill-men and the swords of the Dúnedain lords. Their craft was learned from Númenórean masters and hardened by mountain iron."),
            ("Rangers of the Angle",
             "Your family were Rangers of the Angle, walking the wild hills between the rivers to keep the passes and the roads. Few thanked them and fewer knew their names, but the trolls kept to the high fells while they kept the watch."),
            ("Drifters of the Trollshaws",
             "Your family lived on the edges, drifting between the Trollshaws, the Last Bridge and the empty country around Weathertop. They traded what they found, took what they could, and learned that a stranger's purse was often looser than his tongue."),
        ],
        "youth": [
            ("Rode with the hill riders of Rhudaur.",
             "You joined the few mounted companies of Rhudaur, learning to ride sure-footed ponies over the fells and to strike fast and be gone before the enemy could form."),
            ("Stood watch on the walls of Cameth Brin.",
             "You served in the garrison of Cameth Brin, keeping the night watch on its tower and learning the craft of siege defence from captains who had held it against the orcs of the mountains."),
            ("Ranged the fells with the Rangers of the Angle.",
             "You followed the Rangers of the Angle along the Hoarwell and up into the Ettenmoors, striking at orcs and trolls from cover and learning to read the hills as other men read letters."),
            ("Trained with the Guard of Cameth Brin.",
             "You joined the guard of Cameth Brin, training with greatsword and poleaxe to defend the King and the hall of his fathers on its rock above the hills."),
            ("Lived by your wits in the Trollshaws.",
             "You scraped by in the Trollshaws, where outlaws and troll-hoard robbers made their living. You learned the value of a quick hand, a well-thrown stone and a story nobody could check."),
        ],
        "education": [
            ("Drilled with the sons of the Angle.",
             "You grew up training alongside the children of Rhudaur's Dúnedain houses in the yards of Cameth Brin. You learned sword and bow under lords sworn to the King, and the lineage of Eärendur's youngest son by heart."),
            ("Apprenticed at the Last Bridge.",
             "You spent your youth carrying tallies and running errands at the trading post of the Last Bridge, where the East Road meets the hill country. You learned to bargain with Dwarves, flatter hill-chiefs and never trust a stranger's weights."),
            ("Herded sheep on the fells.",
             "You worked from dawn to dusk on the high fells, driving flocks between the summer and winter pastures and keeping the wolves off them. When the levy was called you were handed an axe and taught to hold a line."),
            ("Apprenticed to a smith of Cameth Brin.",
             "You learned your craft in the forges of Cameth Brin, working mountain iron under a master who still kept the Númenórean ways of tempering steel. By the time you were grown your hands knew the work better than your head."),
            ("Ranged the hills with the Dúnedain.",
             "You followed the Rangers of the Angle as a youngster, learning to move without sound along the Loudwater and through the Trollshaws. You strung your first bow before you could grow a beard, and your first orc fell to it soon after."),
            ("Scrounged along the Great East Road.",
             "You grew up on the road between the Last Bridge and Weathertop, running messages for coin and selling what travellers left behind. The road taught you quick hands, a quicker tongue and when to vanish."),
        ],
        "adulthood": [
            ("You held the Ettendale against a host from the mountains.",
             "When orcs and hill-trolls came down out of the Ettenmoors in force, you held your line in the Ettendale until the King's men came. Your name was spoken in the hall at Cameth Brin."),
            ("You won the Dwarves of the Iron Hills as partners in trade.",
             "When the Dwarves on the East Road wanted a safe crossing of the Hoarwell, you bargained with them until their caravans paid tolls at the Last Bridge and their iron came to Cameth Brin. The King's treasurer does not forget who filled his chests."),
            ("You brought the flocks down from the fells before the snows.",
             "When an early winter fell on the high pastures, you gathered the herders and drove every flock down to the valleys before the passes closed. The villages remember."),
            ("You forged an axe presented to a lord of the Angle.",
             "A lord of the Angle had heard of your work and commissioned a war-axe for the day he took his oath. When he raised it before the assembled guard and it rang true, you knew your name was made."),
            ("You guided a Ranger company out of an ambush in the Trollshaws.",
             "When a Ranger patrol was cut off among the troll-holes of the Trollshaws, you led them out by a path no troll had found, losing not a single man. Their captain clasped your arm and named you a friend of the Dúnedain."),
            ("You uncovered a band of troll-hoard robbers working out of the Last Bridge.",
             "For months gold from the old troll-hoards had been turning up in the back rooms of the Last Bridge. You traced the trade, found the men behind it and handed them to the King's wardens, keeping a little of the gold for your trouble."),
        ],
    },
    # Ered Luin: Thorin's Halls under King Farin, the Broadbeams of Belegost, the Firebeards of
    # Nogrod, the iron of Mount Dolmed, the Dwarf-road east through the Shire to Bree, trade with
    # Lindon to the west and the goblins of Blue Craig to the north.
    "eredluin": {
        "parents": [
            ("Noble of Thorin's Halls",
             "Your family belonged to the House of Borin, kin to King Farin, wielding great axes and leading the warriors who keep the honour of Thorin's Halls in the Blue Mountains."),
            ("Master Smith of Nogrod",
             "Your family kept a forge in Nogrod, where the Firebeards have worked iron since the Elder Days. Their blades and mail went west to the Elves of Lindon and east along the Dwarf-road, and every piece fetched its price."),
            ("Miner of Mount Dolmed",
             "Your family worked the iron mines beneath Mount Dolmed, following the ore into the roots of the Blue Mountains, shoring the galleries against collapse and selling the ore by the cartload in Thorin's Halls."),
            ("Broadbeam Warrior",
             "Your family fought among the Broadbeams of Belegost, mastering axe and shield and holding the mountain gates when the goblins of Blue Craig came down from the north."),
            ("Hoard-Keeper of the Halls",
             "Your family kept the hoards and storehouses of Thorin's Halls, reckoning the kingdom's wealth and planning how a mountain hold is supplied and defended through a hard winter."),
            ("Warden of the Dwarf-road",
             "Your family walked the Dwarf-road east through the Shire to Bree, tracking orcs and wolves with the bow and guarding the caravans that keep the Blue Mountains in trade."),
        ],
        "youth": [
            ("Served in the King's Guard of Thorin's Halls.",
             "You pledged your loyalty to King Farin, serving in the guard of Thorin's Halls and learning to wield axe and long-hafted mattock in defence of the mountain halls."),
            ("Defended the gates of Belegost.",
             "As part of the garrison of Belegost, you manned the crossbows on the gate-towers and held the passes against all comers, learning the craft of dwarven siege defence."),
            ("Trained with the Firebeard hewers.",
             "You joined the hewers of the Firebeards, the shock troops of Nogrod, learning to hurl throwing axes and fight in the narrow corridors of the mountain halls."),
            ("Scouted the slopes below Blue Craig.",
             "You ranged the northern slopes of the Blue Mountains, where the goblins of Blue Craig hold their caves, tracking raiders and wolves and keeping watch on the passes."),
            ("Stood in the vanguard of the Broadbeams.",
             "You trained in the vanguard of the Broadbeams, mastering the long spear and the two-handed axe in the close formations of the dwarven host."),
            ("Marched with the caravan guards on the Dwarf-road.",
             "You marched with the trading caravans along the Dwarf-road through the Shire to Bree, learning the ways of commerce and the art of defending valuable cargo."),
        ],
        "education": [
            ("Trained in the great hall with the King's axe-brothers.",
             "You practised combat alongside the young warriors of King Farin's household, learning to swing the two-handed axe in the echoing training halls deep in the Blue Mountains. Every scar was a lesson; every lesson was a point of pride."),
            ("Apprenticed at the forges of Nogrod.",
             "You spent your youth at the smith's bench in Nogrod, learning to work the iron of the Blue Mountains in forges older than any kingdom of Men. When the work was done you carried finished blades to the Elves of Lindon, who paid in silver and in patience."),
            ("Worked the iron mines of Mount Dolmed.",
             "You swung a pick in the deep galleries under Mount Dolmed before your beard had grown, learning the hidden passages of the mountain and the engineering of tunnels and supports. The deep stone became as familiar to you as open sky."),
            ("Drilled with the Broadbeam shieldwall.",
             "You trained in the close-quarters style of the Broadbeams of Belegost, learning to brace a shield and swing an axe in tight tunnel formations. Your short stride was an advantage in the corridors where taller folk stumbled."),
            ("Counted the stores of Thorin's Halls.",
             "You helped tally the iron, grain and coin of Thorin's Halls from an early age, learning to keep ledgers and to supply a mountain hold through the long winters. The work made you methodical and gave you a strategist's eye for resources."),
            ("Patrolled the foothills and the Dwarf-road.",
             "You ranged the eastern foothills of the Blue Mountains with the outer watch, learning to track orc raiders and shoot from cover among the pines along the Dwarf-road. The mountain air sharpened your senses and distance became your ally."),
        ],
        "adulthood": [
            ("You fought off a goblin raid from Blue Craig alone.",
             "Returning from an outer patrol, you came upon a band of Blue Craig goblins that had slipped past the watch. There was no time to get help. You held the narrow path with your axe until the alarm reached the gate. Three goblins did not reach it."),
            ("You struck the best trade agreement with Lindon in a generation.",
             "The price of Dwarvish iron at the Grey Havens had not been revised in a lifetime of Men. You assembled the facts and put the case to the Elves of Lindon with such clarity that the agreement was rewritten within the month, and King Farin's treasurer learned your name."),
            ("You opened a new seam of iron beneath the Blue Mountains.",
             "The old maps showed a collapse, but you followed the sound of water in the deep stone and found the gallery intact behind the rubble. The seam within was rich, and the engineers who mapped it added your name to the survey record of Thorin's Halls."),
            ("You held the shieldwall at the gates of Belegost.",
             "When a goblin host from Blue Craig pushed into the lower gates, you anchored your section of the shieldwall while others buckled, buying the counterattack time to form. The captain of the Broadbeams noted your name in the battle record, which is the closest a Dwarf gets to poetry."),
            ("You balanced the hoard-reckoning after years of discrepancy.",
             "The ledgers of Thorin's Halls had been misaligned for years, with losses no one could explain. You spent three months cross-referencing records and found the error: a simple conversion mistake compounded across twenty years of entries. The King's treasurer shook your hand."),
            ("You tracked a band of orcs on the Dwarf-road back to its lair.",
             "The band had struck three caravans on the Dwarf-road east of the mountains and slipped away before anyone could follow. You went alone into the hills, found the camp, counted them, sketched the approach, and brought the report back in time for the wardens to act on it."),
        ],
    },
    # Anduin Vale: the Beornings and Woodmen of the Vales of Anduin under Grimbeorn, son of Beorn,
    # in Beorn's Hall; the Old Ford, the Carrock, the High Pass and the Gladden; Mirkwood's Elves
    # to the east, the Misty Mountains' goblins to the west, Dol Guldur and Lothlórien to the south.
    "anduinvale": {
        "parents": [
            ("House of Beorn",
             "Your family belonged to the House of Beorn, kin and companions to Grimbeorn, carrying great axes in the hall of Beorn and riding the sturdy ponies of the Vales wherever the chieftain's word sent them."),
            ("Traders of the Old Ford",
             "Your family kept a trading house at the Old Ford, where the road from the High Pass meets the river. Honey, hides and timber went across to the Woodmen and the Elves of Mirkwood, and every cart paid its due."),
            ("Free Farmers of the Vales",
             "Your family farmed the rich land between the Carrock and the Gladden, keeping cattle, bees and a spear by the door, for wolves and goblins came down from the Misty Mountains more often than the rain."),
            ("Carpenters of Beorn's Hall",
             "Your family were carpenters and axe-smiths in Beorn's Hall, raising the long timber houses of the Vales and forging the broad axes the Beornings are known for."),
            ("Hunters of the Mirkwood Edge",
             "Your family hunted the eaves of Mirkwood east of the river, taking deer and boar and keeping watch on the trees for spiders and the servants of Dol Guldur."),
            ("Drifters of the Gladden",
             "Your family drifted along the Gladden and the banks of the Anduin, trading what they found, taking what they could, and learning that a ferryman hears more than he says."),
        ],
        "youth": [
            ("Rode messages for Grimbeorn.",
             "You rode as a runner for the chieftain, carrying his word between Beorn's Hall, the Old Ford and the Carrock, and learned to read the Vales from the back of a pony."),
            ("Stood watch at the High Pass.",
             "You served in the garrison that watches the High Pass, shooting from the rocks at goblin raiders and learning how a few bowmen can hold a narrow way."),
            ("Fought in the Gladden levy.",
             "You fought in the river-levy of the Gladden, throwing hand-axes from the reeds and closing with the blade when the raiders from Dol Guldur came near the water."),
            ("Tracked wolves and orcs along the Mirkwood edge.",
             "You rode the forest margin east of the Anduin, tracking wolves and orc-bands to their dens and learning to shoot from the saddle between the trees."),
            ("Joined the axe-men of Beorn's Hall.",
             "You took the axe among the household men of Beorn's Hall, learning to fight shoulder to shoulder with spear and blade in the yard under the chieftain's eye."),
            ("Travelled with the river-traders.",
             "You went up and down the Anduin with the river-traders, minding boats and cargo, and learned how to pass a toll, a watchman and a doubtful stranger."),
        ],
        "education": [
            ("Trained in the hall of Grimbeorn.",
             "You grew up among the chieftain's household in Beorn's Hall, swinging the great axe against posts and learning to sit a pony in full gear. Every bruise was a lesson, and the old bear-tales taught you what a Beorning owes his kin."),
            ("Kept tallies at the Old Ford.",
             "You spent your youth counting carts and bales at the Old Ford market, learning to bargain with Woodmen, Elves and the occasional Dwarf, and to know a fair price from a quick tongue."),
            ("Tended the bees and cattle of the Vales.",
             "You worked from dawn to dusk among the hives and herds below the Carrock, hauling water and mending fences. When the levy was called you were handed a spear and taught to hold a line."),
            ("Learned the woodwright's craft in Beorn's Hall.",
             "You learned your trade in the timber yards of Beorn's Hall, shaping beams and hafting axes under a master who judged every cut by eye. By the time you were grown your hands knew the work better than your head."),
            ("Hunted the Mirkwood borders and the Carrock slopes.",
             "You ranged the edge of Mirkwood and the stony slopes of the Carrock with the hunters, learning to move without sound and to loose a bow at a shadow. Your first goblin fell to it soon after."),
            ("Scraped by on the banks of the Gladden.",
             "You grew up among the reed-huts and landings of the Gladden, running errands for coin and selling what the river brought. The water taught you quick hands, a quicker tongue and when to vanish."),
        ],
        "adulthood": [
            ("You held the Carrock ford beside Grimbeorn.",
             "When a goblin host came down from the High Pass to cut the Vales in two, you stood in the ford with the axe-men of the House of Beorn until the Vale Riders came round their flank. Your name was spoken in the hall that night."),
            ("You opened a free market at the Old Ford.",
             "You brought Beornings, Woodmen and the Elves of Mirkwood to one table and settled the tolls at the Old Ford, so that honey and timber crossed the river without a quarrel. Grimbeorn's steward does not forget who filled the storehouses."),
            ("You rebuilt the farms of Eastbank after a goblin raid.",
             "When raiders burned the farms of Eastbank, you gathered the neighbours, raised the barns before the first frost and kept watch on the fields until the harvest was in. The villages remember."),
            ("You raised the gates of Gladden Hold.",
             "You framed and hung the great timber gates of Gladden Hold, hewing the beams yourself. When the first raiders broke against them and held, the garrison named the gates after you."),
            ("You guided families out of the shadow of Dol Guldur.",
             "When the shadow in the south grew and Woodmen families fled north through the trees, you led them across the river by paths no orc scout knew, and lost not one child. Grimbeorn gave you his hand."),
            ("You broke a ring of goblin fences at the Old Ford.",
             "For months stolen goods from the Misty Mountains had turned up in the back rooms along the river. You traced the trade, found the men behind it and handed them to the wardens, keeping a little of the silver for your trouble."),
        ],
    },
    # Angaladh: the Woodmen of western Mirkwood under King Ingomar of the House of Angaladh, in
    # Woodmen-town; Ironwood Hall, the Forest Gate, Thornhold and the watch on the Emyn Duir; the
    # Forest Road, spiders and wolves under the trees, Dol Guldur to the south, the Anduin and the
    # Beornings to the west. A realm of foot soldiers: no riders.
    "angaladh": {
        "parents": [
            ("House of Angáladh",
             "Your family belonged to the House of Angáladh, kin to King Ingomar, bearing great axes in his hall at Woodmen-town and leading the forest levies along the Forest Road."),
            ("Traders of Woodmen-town",
             "Your family kept a trading house in Woodmen-town, where timber, furs and honey came in from the clearings and went out along the Forest Road, and where every load paid its toll."),
            ("Clearing Farmers",
             "Your family worked a clearing hacked out of the forest, felling trees in winter and sowing in spring, with a spear by the door because spiders and wolves came out of the trees more often than the rain."),
            ("Woodwrights of Ironwood Hall",
             "Your family were woodwrights and axe-smiths at Ironwood Hall, felling the dark heartwood the hall is named for and hafting the broad axes of the Woodmen."),
            ("Woodwards of the Forest Road",
             "Your family were woodwards of the Forest Road, walking its length with bow and hatchet to keep the way open and the spiders back. Few thanked them, but the road stayed passable."),
            ("Outcasts of Thornhold",
             "Your family lived on the thorny edges of the Thornhold marches, taking what the forest and the road gave, and learned that a stranger's purse was often looser than his tongue."),
        ],
        "youth": [
            ("Ran dispatches for King Ingomar.",
             "You carried the king's word between Woodmen-town and the outlying holds, running the forest trails from hold to hold."),
            ("Manned the palisade of the Forest Gate.",
             "You served on the wall of the Forest Gate, shooting from the platforms at wolves and goblins on the road below and learning how few bowmen can hold a narrow way."),
            ("Fought in the Thornhold levy.",
             "You fought in the levy of Thornhold, hurling hand-axes from behind the thorn-hedges and closing with the blade when raiders came through the gaps."),
            ("Hunted spiders and wolves under the Emyn Duir.",
             "You ranged the dark trees beneath the Emyn Duir, tracking wolf-packs and burning spider-nests, and learned to shoot at a running shadow between the trunks."),
            ("Joined the spear-line of Woodmen-town.",
             "You took the spear in the household guard of Woodmen-town, learning to stand shoulder to shoulder in the narrow ways between the trees, where a line of spears stops what a charge cannot."),
            ("Peddled goods along the Forest Road.",
             "You went up and down the Forest Road with a pack of knives, thread and honey-cakes, and learned how to pass a hungry stranger, a toll-warden and a doubtful bargain."),
        ],
        "education": [
            ("Trained in the hall of King Ingomar.",
             "You grew up among the king's household in Woodmen-town, swinging the great axe against stumps and learning to move through the trees in full gear. Every bruise was a lesson, and the old forest songs taught you what the Woodmen owe each other."),
            ("Counted timber in the yards of Woodmen-town.",
             "You spent your youth tallying logs and planks in the timber yards of Woodmen-town, learning to bargain with Beornings and caravan-masters and to tell sound wood from rot."),
            ("Worked the clearings round Thornhold.",
             "You worked from dawn to dusk in the clearings around Thornhold, hauling stumps and driving cattle out of the shadow of the trees. When the levy was called you were handed a spear and taught to hold a line."),
            ("Learned the woodwright's craft at Ironwood Hall.",
             "You learned your trade at Ironwood Hall, splitting the dark heartwood and hafting axes under a master who tested every handle with a blow. By the time you were grown your hands knew the work better than your head."),
            ("Hunted the forest trails beneath the Emyn Duir.",
             "You followed the woodwards under the Emyn Duir, learning to move without sound over the leaf-litter and to loose a bow at a shadow. Your first spider fell to it soon after."),
            ("Scraped by around the Forest Gate.",
             "You grew up among the lean-tos outside the Forest Gate, running errands for travellers and selling what the road dropped. The road taught you quick hands, a quicker tongue and when to vanish."),
        ],
        "adulthood": [
            ("You held the Forest Gate beside King Ingomar.",
             "When a host of orcs came up from Dol Guldur to force the Forest Gate, you stood in the passage with the king's spearmen until the woodwards came round through the trees. Your name was spoken in the hall that night."),
            ("You opened a timber market with the Beornings.",
             "You crossed the Anduin and put the case for Angáladh's timber to the Beornings with such plainness that a regular market was set up within the month. The king's steward learned your name."),
            ("You drove the spiders out of Thornhold's old fields.",
             "When the forest crept back over the abandoned clearings of Thornhold, you led the farmers in with fire and spears, cleared the nests and brought in a harvest where none had grown in a generation."),
            ("You raised the new gate-beams of Ironwood Hall.",
             "You chose, felled and hauled the ironwood for the great beams of the hall's gate, hewing them yourself. When the first raiders broke against them and held, the garrison named the gate-beams after you."),
            ("You brought a woodward patrol out of a spider-hollow.",
             "When a woodward patrol was cut off in a hollow under the Emyn Duir, you led them out by a deer-trail no spider watched, losing not a single man. Their captain clasped your arm and named you a friend of the woodwards."),
            ("You broke a band of fences working the Forest Road.",
             "For months looted goods had been turning up in the back rooms of Woodmen-town. You traced the trade, found the men behind it and handed them to the king's wardens, keeping a little of the silver for your trouble."),
        ],
    },
}

# Words that must not survive the remap, per clone source. A leftover here is the defect the
# new-factions RCA recorded: an id rename is case-sensitive and leaves player-facing text naming
# the source faction.
FORBIDDEN_FOR = {
    "gundabad": ("Gundabad", "Pale Uruk", "pale orc", "Pale Orc"),
    "goblin": ("Goblin-town", "High Pass"),
    "rivendell": ("Rivendell", "Imladris"),
    # The same Gondor identity words ClonedCultureFlavorTests holds Arthedain to.
    "gondor": ("Gondor", "Minas Tirith", "White City", "Osgiliath", "Steward", "Pelennor", "Ithilien",
               "Dol Amroth", "Swan Knight", "Faramir", "Anduin", "Pelargir", "Ephel Duath", "Tower Guard"),
    # The Lonely Mountain's places and people. Ered Luin is the other Dwarf kingdom, so its text
    # must not borrow Erebor's halls, its King under the Mountain or its neighbours in Dale.
    "erebor": ("Erebor", "Lonely Mountain", "Dale", "Esgaroth", "Lake-town", "Smaug", "Iron Hills",
               "Dain", "Dáin", "Ironfoot", "Ravenhill", "Arkenstone", "Under the Mountain"),
    # Dale's places and people: both realms are cut from it and must not borrow its town, its
    # king or its neighbours.
    "sturgia": ("Dale", "Bard", "Esgaroth", "Lake-town", "Lonely Mountain", "Girion", "Erebor",
                "King Brand", "Long Lake", "Smaug"),
}

# Per-culture display-text remap of the gundabad flavor (longest phrase FIRST so substrings don't
# double-substitute). goblin = Goblin-town in the High Pass; mistymountainorcs = the orc-host of the
# Misty Mountains / Moria. "Orc" left intact (orcs + goblins coexist in lore).
SUBS = {
    "goblin": [
        ("Warg riders of Gundabad", "warg-packs of the High Pass"),
        ("war-halls of Mount Gundabad", "tunnel-halls of Goblin-town"),
        ("Mount Gundabad", "the High Pass"),
        ("forges of Gundabad", "warrens of Goblin-town"),
        ("garrison of Gundabad", "warrens of Goblin-town"),
        ("Nobles of Gundabad", "Chiefs of Goblin-town"),
        ("Smiths of Gundabad", "Tinkerers of Goblin-town"),
        ("Gundabad had smiths", "Goblin-town had tinkerers"),
        ("of Gundabad", "of Goblin-town"),
        ("Gundabad", "Goblin-town"),
    ],
    "mistymountainorcs": [
        ("Warg riders of Gundabad", "warg-riders of the Misty Mountains"),
        ("war-halls of Mount Gundabad", "war-halls of Moria"),
        ("Mount Gundabad", "the Misty Mountains"),
        ("forges of Gundabad", "deep forges of Moria"),
        ("garrison of Gundabad", "garrison of Moria"),
        ("Nobles of Gundabad", "Warlords of the Misty Mountains"),
        ("Smiths of Gundabad", "Smiths of Moria"),
        ("Gundabad had smiths", "Moria had smiths"),
        ("of Gundabad", "of the Misty Mountains"),
        ("Gundabad", "the Misty Mountains"),
    ],
    # Blue Craig: western goblins of the Ered Luin, cut off from their Goblin-town kin by the whole
    # width of Eriador. Their neighbours are the Dwarves of the Blue Mountains and the Elves of
    # Mithlond, so the flavour moves west even though the people do not change.
    "bluecraig": [
        ("warg-packs of the High Pass", "warg-packs of the Blue Mountains"),
        ("tunnel-halls of Goblin-town", "crag-halls of Blue Craig"),
        ("warrens of Goblin-town", "warrens of Blue Craig"),
        ("Chiefs of Goblin-town", "Chiefs of Blue Craig"),
        ("Tinkerers of Goblin-town", "Tinkerers of Blue Craig"),
        ("Goblin-town had tinkerers", "Blue Craig had tinkerers"),
        ("of Goblin-town", "of Blue Craig"),
        ("the High Pass", "the Ered Luin"),
        ("Goblin-town", "Blue Craig"),
    ],
    # Lindon: Círdan's Falathrim at the Grey Havens. Imladris is a hidden valley of loremasters;
    # Mithlond is a haven of shipwrights, so the imagery moves from the vale to the sea.
    "lindon": [
        ("hidden valley of Imladris", "grey havens of Mithlond"),
        ("Last Homely House", "Grey Havens"),
        ("valley of Imladris", "firth of Lune"),
        ("house of Elrond", "quays of Círdan"),
        ("of Imladris", "of Mithlond"),
        ("of Rivendell", "of Lindon"),
        ("Imladris", "Mithlond"),
        ("Rivendell", "Lindon"),
    ],
    "arthedain": [],  # AUTHORED text replaces the remap entirely
    "cardolan": [],  # AUTHORED (#978)
    "rhudaur": [],  # AUTHORED (#978)
    "eredluin": [],  # AUTHORED
    "anduinvale": [],  # AUTHORED
    "angaladh": [],  # AUTHORED
}

CC_STRINGS = os.path.join(os.path.dirname(CC), "taom_cc_strings.xml")


def prepare_cc_strings(entries):
    """(the new taom_cc_strings.xml bytes, or None when no row is missing; how many rows were
    missing) for the `taom_cc_<string_id>_text` / `_desc` rows of `entries` (main() passes every
    entry of the cultures in NEW, not only those this run minted). The text is parsed before it is
    returned, and nothing is written: main() writes every output only after all of them are ready.

    NarrativeMenuBuilder composes those keys at runtime from each entry's string_id, so
    tools/harvest_literal_loc_keys.py cannot see them and a run that skips this leaves every new
    option English-only in all eleven other languages. Idempotent per key. The file carries mixed
    line endings (CRLF and CR CR LF, .claude/rules/xml-data.md), so the new rows take the ending of
    the line they are inserted after."""
    from xml.sax.saxutils import escape
    raw = open(CC_STRINGS, "rb").read()
    bom = raw.startswith(b"\xef\xbb\xbf")
    text = raw.decode("utf-8-sig")
    rows = []
    for e in entries:
        for suffix, value in (("text", e["text"]), ("desc", e["description"])):
            key = f"taom_cc_{e['string_id']}_{suffix}"
            if f'id="{key}"' in text:
                continue
            rows.append(f'\t<string id="{key}" text="{{={key}}}{escape(value, {chr(34): "&quot;"})}" />')
    if not rows:
        return None, 0
    close = text.rfind("</strings>")
    line_start = text.rfind("\n", 0, close) + 1
    m = re.search(r"(\r*\n)$", text[:line_start])
    nl = m.group(1) if m else "\n"
    text = text[:line_start] + "".join(r + nl for r in rows) + text[line_start:]
    import xml.etree.ElementTree as ET
    ET.fromstring(text.encode("utf-8"))
    return (b"\xef\xbb\xbf" if bom else b"") + text.encode("utf-8"), len(rows)


def remap(s, culture):
    if not isinstance(s, str):
        return s
    for old, new in SUBS[culture]:
        s = s.replace(old, new)
    return s


def clone_entry(entry, culture):
    out = {}
    for k, v in entry.items():
        if k == "string_id":
            out[k] = v.replace(SRC_FOR[culture], culture)
        elif k == "culture_id":
            out[k] = culture
        elif k in ("text", "description"):
            out[k] = remap(v, culture)
        else:
            out[k] = v
    return out


def fmt_entry(d, nl):
    """Serialize one entry to match the existing style: 4-space '{'/'}' indent, 8-space keys,
    each value via json.dumps so arrays stay inline (["a", "b"])."""
    items = list(d.items())
    lines = ["    {"]
    for i, (k, v) in enumerate(items):
        comma = "," if i < len(items) - 1 else ""
        lines.append("        " + json.dumps(k) + ": " + json.dumps(v, ensure_ascii=False) + comma)
    lines.append("    }")
    return nl.join(lines)


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    mode = ap.add_mutually_exclusive_group()
    mode.add_argument("--apply", action="store_true",
                      help="write the menus and taom_cc_strings.xml (default: dry run)")
    mode.add_argument("--check", action="store_true",
                      help="write nothing; exit 1 if a culture in NEW still lacks entries or rows")
    args = ap.parse_args(argv)

    pending = []  # every entry this run adds (or, in a dry run, would add)
    menu_writes = []  # (path, new text): written only once every output is prepared and parsed
    final_entries = []  # each menu's NEW-culture entries as this run leaves it, for the strings
    for menu in MENUS:
        path = os.path.join(CC, menu + "_menu.json")
        raw = open(path, encoding="utf-8", newline="").read()
        nl = "\r\n" if "\r\n" in raw else "\n"
        data = json.loads(raw)

        # Skip per CULTURE, not per file. A single file-level guard meant that once Goblin-town had
        # its entries the whole file was considered done, so Blue Craig and Lindon could never be
        # added to it — the check has to ask the question separately for each culture.
        blocks = []
        for c in NEW:
            if f'"culture_id": "{c}"' in raw:
                print(f"  {menu}_menu.json: {c} already present — skipping")
                continue
            src_culture = SRC_FOR[c]
            src = [e for e in data if e.get("culture_id") == src_culture]
            if not src:
                # Nothing to clone leaves this culture with zero options, so its CC stage renders
                # blank (the bug this script exists to prevent): a failure in every mode, raised
                # before anything is written. A source that is itself new this run means NEW is out
                # of order, since `data.extend(cloned)` below shows a chained source (gundabad ->
                # goblin -> bluecraig) only to the cultures after it.
                if src_culture in NEW:
                    raise SystemExit(
                        f"{menu}/{c}: clone source '{src_culture}' is also new this run and has not "
                        f"been generated yet — order NEW so a source precedes its dependents")
                raise SystemExit(f"{menu}/{c}: clone source '{src_culture}' has no entries to clone; "
                                 f"fix SRC_FOR or restore the source's entries")
            if c in AUTHORED:
                texts = AUTHORED[c][menu]
                if len(texts) != len(src):
                    raise SystemExit(f"{menu}/{c}: {len(texts)} authored entries for {len(src)} source entries")
                cloned = []
                for e, (text, desc) in zip(src, texts):
                    d = clone_entry(e, c)
                    d["text"], d["description"] = text, desc
                    cloned.append(d)
            else:
                cloned = [clone_entry(e, c) for e in src]
            made = [fmt_entry(d, nl) for d in cloned]
            bad = [b for b in made if any(w in b for w in FORBIDDEN_FOR[src_culture])]
            if bad:
                raise RuntimeError(
                    f"{menu}/{c}: source-culture leftover survived remap in {len(bad)} entrie(s)")
            blocks += made
            pending += cloned
            # Make this culture's entries visible to later cultures in the same run, so a chained
            # source (gundabad -> goblin -> bluecraig) resolves in ONE invocation instead of
            # silently requiring two.
            data.extend(cloned)

        final_entries += [e for e in data if e.get("culture_id") in NEW]
        if not blocks:
            continue
        # textual splice: insert before the closing ']' (comma after the existing last entry)
        idx = raw.rfind("]")
        before = raw[:idx].rstrip()          # ends at the last entry's '}'
        after = raw[idx:]                     # ']' + trailing newline
        new_text = before + "," + nl + (("," + nl).join(blocks)) + nl + after
        json.loads(new_text)  # parse before write: a splice that breaks the file is never written
        menu_writes.append((path, new_text))
        verb = "+" if args.apply else "would add "
        print(f"  {menu}_menu.json: {verb}{len(blocks)} entries; valid JSON, no leftovers")
    # Register every narrative entry of these cultures that lacks its rows, not only this run's: a
    # run that added the JSON but not the strings (or a reverted strings file) is repaired by a plain
    # re-run instead of being reported as nothing to do. `final_entries` is each menu as this run
    # leaves it, so a dry run counts the rows it would add.
    strings_data, rows = prepare_cc_strings(final_entries)
    if args.apply:
        # Every output is prepared and parsed by now (tools/README.md "XML I/O convention"), so a
        # failure above leaves all five files as they were.
        for path, new_text in menu_writes:
            with open(path, "w", encoding="utf-8", newline="") as f:
                f.write(new_text)
        if strings_data is not None:
            open(CC_STRINGS, "wb").write(strings_data)
        print(f"  taom_cc_strings.xml: +{rows} row(s)")
        print("CC menus done.")
        return 0
    print(f"  taom_cc_strings.xml: {rows} row(s) to add")
    if args.check:
        return 1 if pending or rows else 0
    print("DRY RUN: re-run with --apply to write" if pending or rows else "nothing to do")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
