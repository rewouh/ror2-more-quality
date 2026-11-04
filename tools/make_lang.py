"""Generates lang/en/Items.txt and lang/en/Equipment.txt for MoreQuality.

Two kinds of entries, like Quality's own descriptions:
  - Rewritten: quality changes a number Starstorm2 already shows, so the whole description is
    written out with the changed values in place, marked with Quality's diamond sprite.
  - Added (add()): quality only adds a new effect. "[[BASE]]" is replaced at display time with
    Starstorm2's own description (see src/BaseText.cs), followed by the new line.

Every tier has the same effects; only the values change.
Base numbers come from the ModdedMulitplayer profile's Starstorm2 config. If it changes, update them here.
Also adds LookingGlass names and descriptions for this mod's buffs.

Run:  python tools/make_lang.py
"""
import json
import os

Q = '<sprite name="Quality">'
TIERS = ["UNCOMMON", "RARE", "EPIC", "LEGENDARY"]
BASE = "[[BASE]]"


def style(name):
    return lambda text: "<style=" + name + ">" + text + "</style>"


dmg = style("cIsDamage")
util = style("cIsUtility")
heal = style("cIsHealing")
health = style("cIsHealth")


def stack(text):
    return "<style=cStack>(" + text + ")</style>"


def q(text, fmt=util):
    """A value quality changed."""
    return Q + " " + fmt(text)


def qs(values, fmt=util):
    """The same q() for each tier's value."""
    return [q(v, fmt) for v in values]


def per_tier(template, *columns):
    """template.format(*values) for each tier; each column holds the 4 tier values."""
    return [template.format(*[c[i] for c in columns]) for i in range(4)]


def add(lines):
    return [BASE + "\n" + line for line in lines]


# name -> (pickup line, [uncommon, rare, epic, legendary] descriptions)
ITEMS = {}
EQUIPMENT = {}

# ---------------------------------------------------------------- white

ITEMS["ArmedBackpack"] = ("Fires a much bigger, exploding missile.", per_tier(
    "Chance on taking damage to fire a {0} missile for {1} " + stack("+400% per stack") + " base damage that " + Q + " explodes in a {2} radius. Chance scales with damage taken.",
    [q("x6 size"), q("x10.5 size"), q("x18 size"), q("x36 size, slow-moving")],
    qs(["600%", "900%", "1400%", "2400%"], dmg),
    [util("4m"), util("7m"), util("11m"), util("20m")]))

ITEMS["BloodTester"] = ("Healing also gives you a rush.", add(per_tier(
    "Each heal also grants " + util("Blood Rush") + " for {0} " + stack("+1s per stack") + ": {1} movement speed, {2} attack speed, {3} damage and {4} armor.",
    qs(["3s", "4s", "6s", "10s"]), qs(["+15%", "+30%", "+60%", "+120%"]),
    qs(["+10%", "+20%", "+50%", "+100%"], dmg), qs(["+5%", "+10%", "+25%", "+50%"], dmg),
    qs(["+5", "+10", "+25", "+50"], heal))))

ITEMS["CoffeeBag"] = ("More beans; beans fly to you and can cut your cooldowns.", per_tier(
    "{0} chance on hit to drop a coffee bean that grants " + dmg("8% attack speed") + " and " + util("movement speed") + " for " + util("6 seconds") + ". Maximum cap of " + dmg("40%") + " " + stack("+40% per stack") + ".\n"
    "Grabbing a bean while you have at least " + util("5") + " beans reduces " + util("all skill cooldowns") + " by {1} " + stack("{2} per stack") + ". Beans within {3} fly to you.",
    qs(["12%", "14%", "20%", "30%"], dmg), qs(["0.5s", "1s", "2.5s", "5s"]),
    ["+0.5s", "+1s", "+2.5s", "+5s"], qs(["8m", "15m", "35m", "70m"])))

ITEMS["DetritiveTrematode"] = ("Infestations spread when the host dies.", add(per_tier(
    "An infested enemy that dies spreads the infestation to {0} " + stack("{1} per stack") + " {2} within {3}, at any health.",
    qs(["1", "2", "5", "10"]), ["+1", "+2", "+5", "+10"],
    ["enemy", "enemies", "enemies", "enemies"], qs(["12m", "15m", "22m", "30m"]))))

ITEMS["Diary"] = ("Also gives a random quality item when consumed.", add(per_tier(
    "When consumed, also gain a random item of {0} quality.",
    qs(["Uncommon", "Rare", "Epic", "Legendary"]))))

ITEMS["Fork"] = ("More base damage; repeated hits pin enemies in place.", per_tier(
    "Increase your " + dmg("base damage") + " by {0} " + stack("{1} per stack") + ".\n"
    "Every {2} hits on the same enemy " + util("pin") + " it in place for {3}. Pinned enemies take {4} damage from all sources.",
    qs(["20%", "32%", "68%", "128%"], dmg), ["+20%", "+32%", "+68%", "+128%"],
    qs(["6", "5", "3", "2"]), qs(["0.5s", "0.8s", "1.5s", "3s"]), qs(["+10%", "+20%", "+50%", "+100%"], dmg)))

ITEMS["GuardingAmulet"] = ("Hits from behind grant barrier and stun the attacker.", add(per_tier(
    "Hits from behind also grant " + heal("barrier") + " equal to {0} " + stack("{1} per stack") + " of the hit's damage, and " + util("stun") + " the attacker for {2}.",
    qs(["10%", "20%", "50%", "100%"], heal), ["+10%", "+20%", "+50%", "+100%"],
    qs(["0.5s", "1s", "2s", "4s"]))))

ITEMS["Malice"] = ("The splash can keep chaining.", add(per_tier(
    "When the splash runs out of targets, the last enemy hit has a {0} " + stack("{1} per stack") + " chance to pass it on, up to {2} extra times.",
    qs(["10%", "20%", "50%", "90%"]), ["+10%", "+20%", "+50%", "+90%"],
    qs(["2", "4", "10", "20"]))))

ITEMS["MoltenCoin"] = ("Burning enemies drop molten coins.", per_tier(
    "{0} chance to " + dmg("ignite") + " enemies on hit for " + dmg("200%") + " " + stack("+200% per stack") + " TOTAL damage and earn " + util("$1") + " " + stack("+$1 per stack") + ".\n"
    "Burning enemies drop a " + util("molten coin") + " when they die, worth {1} " + stack("{2} per stack") + ". " + util("Scales over time") + ". "
    "Picking it up makes you " + dmg("Molten") + " for {3}, " + dmg("igniting") + " enemies within {4}.",
    qs(["8%", "10%", "16%", "26%"], dmg), qs(["$2", "$4", "$10", "$20"]), ["+$2", "+$4", "+$10", "+$20"],
    qs(["3s", "4s", "6s", "10s"]), qs(["5m", "7m", "11m", "16m"])))

ITEMS["Needles"] = ("More needles; needled enemies become voodoo dolls.", per_tier(
    "Gain " + dmg("5%") + " " + stack("+5% per stack") + " " + dmg("critical chance") + ". " + dmg("Critical strikes") + " have a {0} chance to " + dmg("needle") + " an enemy for {1}" + dmg("x10%") + " " + stack("+10% per stack") + " TOTAL damage, with a {2} chance to make it " + dmg("bleed") + ".\n"
    "A needled enemy becomes a " + util("voodoo doll") + " for good: {3} " + stack("{4} per stack") + " of the damage dealt to it is also dealt to every other voodoo doll.",
    qs(["25%", "30%", "45%", "70%"], dmg), qs(["4", "5", "8", "13"], dmg), qs(["15%", "30%", "75%", "100%"], dmg),
    qs(["5%", "10%", "25%", "50%"], dmg), ["+5%", "+10%", "+25%", "+50%"]))

ITEMS["UniversalCharger"] = ("Refreshes come back faster and overcharge the skill.", per_tier(
    "Every {0} " + stack("-10% per stack") + ", instantly " + util("refresh the cooldown") + " of your next " + util("non-Primary skill") + ", with a {1} chance to refresh " + util("all of them") + " at once.\n"
    "Using a refreshed skill again makes you " + util("Overcharged") + " for " + util("2.5s") + ": {2} " + stack("{3} per stack") + " " + dmg("damage") + ".",
    qs(["16.4 seconds", "15 seconds", "12 seconds", "9 seconds"]), qs(["10%", "20%", "50%", "100%"]),
    qs(["+20%", "+40%", "+100%", "+200%"], dmg), ["+20%", "+40%", "+100%", "+200%"]))

ITEMS["UraniumHorseshoe"] = ("Even faster; softer falls; landings stomp.", per_tier(
    "Increases " + util("movement speed") + " and " + util("jump height") + " by {0} " + stack("{1} per stack") + ". "
    "Take {2} less " + util("fall damage") + ". Landing from a fall " + dmg("stomps") + " for up to {3} base damage in " + util("10m") + " and " + util("stuns") + ".",
    qs(["25%", "40%", "85%", "160%"]), ["+25%", "+40%", "+85%", "+160%"],
    qs(["25%", "50%", "80%", "100%"]), qs(["150%", "300%", "750%", "1500%"], dmg)))

ITEMS["X4"] = ("Your secondary can grant random boosts.", add(per_tier(
    "Using your " + util("secondary skill") + " has a {0} " + stack("+10% per stack") + " chance to grant a random boost for " + util("5s") + ", with a {1} chance of a " + util("second, different one") + ": armor, movement speed, damage, crit, barrier or regeneration.",
    qs(["20%", "40%", "70%", "100%"]), qs(["10%", "20%", "50%", "100%"]))))

# ---------------------------------------------------------------- green

ITEMS["CrypticSource"] = ("A bigger burst that refunds utility cooldown.", per_tier(
    "Upon ending your sprint, unleash a " + dmg("burst of energy") + " in a {0} " + stack("{1} per stack") + " radius for " + dmg("250%") + " " + stack("+250% per stack") + " base damage. "
    "Each enemy in range (up to 3) reduces your " + util("utility cooldown") + " by {2} " + stack("{3} per stack") + ".",
    qs(["16.5m", "18m", "22.5m", "30m"]), ["+3.3m", "+3.6m", "+4.5m", "+6m"],
    qs(["0.5s", "1s", "2.5s", "5s"]), ["+0.5s", "+1s", "+2.5s", "+5s"]))

ITEMS["FieldAccelerator"] = ("The charging zone speeds allies up and slows enemies down.", per_tier(
    "After defeating the boss, the radius of the Teleporter is increased by " + util("20%") + " " + stack("+20% per stack") + " and killing enemies grants {0} " + util("teleporter charge") + ".\n"
    "Inside the charging zone, allies gain {1} " + stack("{2} per stack") + " " + dmg("attack speed") + " and their projectiles fly {3} " + stack("{4} per stack") + " faster, while enemies are " + util("slowed") + " by {5}.",
    qs(["+1.5%", "+2%", "+3.5%", "+6%"]),
    qs(["+5%", "+10%", "+25%", "+50%"], dmg), ["+5%", "+10%", "+25%", "+50%"],
    qs(["10%", "20%", "50%", "100%"]), ["+10%", "+20%", "+50%", "+100%"],
    qs(["10%", "20%", "40%", "70%"])))

ITEMS["HuntersSigil"] = ("A bigger zone that grants crit and hunts enemies.", per_tier(
    "After " + util("standing still") + " for " + util("1") + " second, create a zone that grants " + heal("30") + " " + stack("+15 per stack") + " " + heal("armor") + ", "
    + dmg("30%") + " " + stack("+15% per stack") + " increased " + dmg("damage") + " and {0} " + stack("{1} per stack") + " " + dmg("critical chance") + " to all allies within {2}. The zone lasts until you leave it.\n"
    "Enemies in the zone are " + util("Hunted") + ": they take {3} " + stack("{4} per stack") + " damage from all sources, until {5} after they leave it.",
    qs(["+10%", "+20%", "+50%", "+100%"], dmg), ["+10%", "+20%", "+50%", "+100%"],
    qs(["10m", "12m", "16m", "22m"]),
    qs(["+20%", "+40%", "+100%", "+200%"], dmg), ["+20%", "+40%", "+100%", "+200%"],
    qs(["2s", "3s", "5s", "8s"])))

ITEMS["JetBoots"] = ("Faster recharge, a bigger blast, and a pool of fire.", per_tier(
    "Gain " + util("+1 extra jump") + " that " + dmg("ignites") + " enemies for {0} " + stack("+200% per stack") + " base damage in a " + dmg("7.5m") + " " + stack("+2.5m per stack") + " radius. "
    "Recharges after {1} seconds, with a {2} chance to be ready again right away.\n"
    "Each rocket jump leaves a " + dmg("pool of fire") + " where you took off ({3} base damage).",
    qs(["500%", "600%", "900%", "1400%"], dmg), qs(["8", "6", "4", "2"]),
    qs(["10%", "20%", "50%", "90%"]), qs(["100%", "200%", "500%", "1000%"], dmg)))

ITEMS["LightningOnKill"] = ("Kills can release a friendly jellyfish.", add(per_tier(
    "{0} " + stack("{1} per stack") + " chance on kill to release a " + util("friendly jellyfish") + " that flies to the nearest enemy and explodes for {2} base damage.",
    qs(["10%", "20%", "40%", "70%"]), ["+10%", "+15%", "+20%", "+25%"],
    qs(["300%", "600%", "1500%", "3000%"], dmg))))

ITEMS["LowQualitySpeakers"] = ("The burst launches enemies sky high.", add(per_tier(
    "The burst launches enemies {0} into the air; they take {1} " + dmg("fall damage") + " when they land.",
    qs(["10m", "20m", "40m", "70m"]), qs(["x1", "x1.5", "x2.5", "x4"], dmg))))

ITEMS["StrangeCan"] = ("Intoxicated enemies explode on death.", add(per_tier(
    "Intoxicated enemies " + dmg("explode") + " on death in a radius of " + util("2m") + " {0} per intoxicate stack, applying {1} " + stack("+1 per stack") + " intoxicate to everything hit.",
    qs(["+0.5m", "+1m", "+2.5m", "+5m"]), qs(["1", "2", "5", "10"]))))

ITEMS["WatchMetronome"] = ("Hit on the beat for big crits.", add(per_tier(
    "While charged, the metronome " + util("ticks every second (with a sound and a golden pulse)") + ". Hits in the {0} after each tick are " + dmg("critical strikes") + " dealing {1} " + stack("{2} per stack") + " damage, and the first one has a {3} chance to give back a " + util("charge") + ".",
    qs(["0.2s", "0.3s", "0.45s", "0.6s"]), qs(["+20%", "+40%", "+100%", "+200%"], dmg), ["+20%", "+40%", "+100%", "+200%"],
    qs(["10%", "20%", "50%", "100%"]))))

# ---------------------------------------------------------------- red

ITEMS["DroidHead"] = ("Drones last longer, keep each other alive and explode.", per_tier(
    "Killing an elite enemy spawns a " + dmg("Security Drone") + " " + stack("+100% base damage per stack") + " for {0} " + stack("+10s per stack") + ", with a {1} " + stack("{2} per stack") + " chance to spawn " + util("a second one") + ". "
    "Each new drone adds {3} to your other drones. Spawned drones " + util("inherit the elite type") + " of the slain elite.\n"
    "Drones " + dmg("explode") + " when they expire or are destroyed, dealing {4} base damage within {5}.",
    qs(["30s", "40s", "70s", "120s"], dmg),
    qs(["15%", "30%", "75%", "100%"]), ["+15%", "+30%", "+75%", "+150%"],
    qs(["5s", "10s", "25s", "50s"]),
    qs(["300%", "600%", "1500%", "3000%"], dmg), qs(["10m", "12m", "16m", "22m"])))

ITEMS["ErraticGadget"] = ("Lightning can strike anywhere on the stage, even chests.", per_tier(
    "Your lightning strikes " + dmg("twice") + ". {0} chance on hit to fire " + dmg("chain lightning") + " for " + dmg("300%") + " TOTAL damage on up to " + dmg("1") + " " + stack("+1 per stack") + " targets. "
    "Each gadget strike has a {1} " + stack("+5% per stack") + " chance to also strike a " + util("random enemy anywhere") + " on the stage, "
    "and a {2} chance to strike an " + util("unopened chest") + " anywhere on the stage, " + util("opening it for free") + ".",
    qs(["15%", "20%", "35%", "60%"], dmg), qs(["5%", "10%", "25%", "50%"]), qs(["0.25%", "0.5%", "1.25%", "2.5%"])))

ITEMS["GalvanicCore"] = ("Razed enemies take more damage, get debuffs and arc lightning.", per_tier(
    "{0} " + stack("+10% per stack") + " chance on hit to " + util("stun") + ". Stunned enemies apply " + util("razed") + " to enemies within " + util("15m") + " " + stack("+5m per stack") + ", "
    "reducing their " + health("maximum health") + " by " + health("20%") + ", " + dmg("damage") + " by " + dmg("30%") + ", and " + util("movement speed") + " by " + util("80%") + ". "
    "Razed enemies also take {1} " + stack("{2} per stack") + " damage, get {3} for {4}, and every second " + dmg("arc lightning") + " to the nearest enemy for {5} base damage.",
    qs(["15%", "20%", "35%", "60%"]),
    qs(["+10%", "+20%", "+50%", "+100%"], dmg), ["+10%", "+20%", "+50%", "+100%"],
    [q("1") + " random debuff", q("2") + " random debuffs", q("3") + " random debuffs", q("5") + " random debuffs"],
    qs(["3s", "4s", "6s", "10s"]), qs(["50%", "100%", "250%", "500%"], dmg)))

ITEMS["GreenChocolate"] = ("Triggers on smaller hits, and drops a chocolate to share.", per_tier(
    "When damaged for at least {0} of your max HP, the excess damage is reduced by " + dmg("50%") + ", and also gain a buff that grants {1} base damage and {2} critical chance for " + dmg("6s") + " " + stack("+3s per stack") + ".\n"
    "Each trigger also drops a " + util("chocolate bar") + ". Whoever picks it up is " + util("cleansed") + " of debuffs, and all allies within {3} get the buff for {4}.",
    qs(["18%", "15%", "10%", "5%"], health), qs(["+60%", "+75%", "+125%", "+200%"], dmg), qs(["+25%", "+30%", "+45%", "+70%"], dmg),
    qs(["10m", "15m", "25m", "40m"]), qs(["3s", "5s", "8s", "15s"])))

ITEMS["Insecticide"] = ("Poison can kill outright, and leaves toxic clouds.", add(per_tier(
    "Each poison tick has a {0} " + stack("{1} per stack") + " chance to " + dmg("instantly kill") + " the enemy. Doesn't work on bosses.\n"
    "A poisoned enemy that dies leaves a " + dmg("toxic cloud") + " of {2} for {3}, dealing {4} base damage per second to enemies inside.",
    qs(["0.25%", "0.5%", "1.25%", "2.5%"], dmg), ["+0.25%", "+0.25%", "+0.5%", "+0.5%"],
    qs(["4m", "5m", "7m", "10m"]), qs(["3s", "4s", "6s", "8s"]), qs(["100%", "200%", "500%", "1000%"], dmg))))

ITEMS["NkotasHeritage"] = ("Gives quality items.", add(per_tier(
    "Items it gives are {0} quality.", qs(["Uncommon", "Rare", "Epic", "Legendary"]))))

ITEMS["PortableReactor"] = ("Lasts longer, hits harder, and melts down at the end.", per_tier(
    "Gain " + util("full invulnerability") + " and " + util("100% movement speed") + " for the first {0} " + stack("+40s per stack") + " of a stage, dealing {1} " + stack("{2} per stack") + " " + dmg("damage") + " meanwhile.\n"
    "When it ends, the reactor " + dmg("melts down") + " within {3}, dealing {4} base damage plus {5} of the damage it absorbed.",
    qs(["100s", "120s", "180s", "280s"]),
    qs(["+20%", "+40%", "+100%", "+200%"], dmg), ["+20%", "+40%", "+100%", "+200%"],
    qs(["20m", "25m", "32m", "45m"]), qs(["1000%", "2000%", "5000%", "10000%"], dmg), qs(["100%", "200%", "500%", "1000%"], dmg)))

ITEMS["SwiftSkateboard"] = ("More stacks with attack speed; grind a burning trail.", per_tier(
    util("Sprint") + " in " + util("any direction") + ". Using a " + util("skill") + " grants a buff which provides " + util("+20%") + " " + stack("+15% per stack") + " " + util("movement speed") + " and {0} " + stack("{1} per stack") + " " + dmg("attack speed") + " for " + util("6") + " seconds, up to {2} stacks.\n"
    "At max stacks, leave a " + dmg("burning trail") + " dealing {3} base damage per second.",
    qs(["+3%", "+6%", "+15%", "+30%"], dmg), ["+3%", "+6%", "+15%", "+30%"],
    qs(["5", "6", "9", "14"]), qs(["100%", "200%", "500%", "1000%"], dmg)))

# ---------------------------------------------------------------- boss

ITEMS["ShackledLamp"] = ("Fires more often; kills fire again.", per_tier(
    "Every {0} primary uses, fire a haunted projectile for " + dmg("200%") + " " + stack("+200% per stack") + " base damage. Kills have a {1} " + stack("+10% per stack") + " chance to fire " + util("another") + " from the victim at the nearest enemy.",
    qs(["4", "3", "2", "1"], dmg), qs(["10%", "20%", "50%", "90%"])))

# ---------------------------------------------------------------- equipment

EQUIPMENT["BackThruster"] = ("Lasts longer; ram and stun enemies at speed.", per_tier(
    "For {0} seconds, rapidly build up to " + util("200% movement speed") + " when moving in a straight line. At speed, " + dmg("ramming") + " enemies deals {1} base damage and " + util("stuns") + " them for {2}.",
    qs(["10", "12", "16", "24"]), qs(["300%", "600%", "1500%", "3000%"], dmg), qs(["0.5s", "1s", "2s", "4s"])))

EQUIPMENT["CloakingHeadband"] = ("Your first strikes out of cloak are big crits.", add(per_tier(
    "Your first hit while cloaked, and every hit in the {0} after it, is a guaranteed " + dmg("critical strike") + " dealing {1} damage.",
    qs(["0.2s", "0.4s", "1s", "2s"]), qs(["+50%", "+100%", "+250%", "+500%"], dmg))))

EQUIPMENT["GreaterWarbanner"] = ("Bigger banners, more of them, slammed into the ground.", per_tier(
    "Place a greater warbanner {0} which grants " + heal("+50% health regeneration") + ", " + dmg("20% critical strike chance") + ", and " + util("50% skill cooldown reduction") + ". Can place up to {1} at a time.\n"
    "The banner " + dmg("slams") + " into the ground, dealing {2} base damage and " + util("stunning") + " enemies within {3} for {4}.",
    ["with a " + q("20%") + " larger radius", "with a " + q("40%") + " larger radius", "with a " + q("100%") + " larger radius", "with a " + q("200%") + " larger radius"],
    [util("1"), q("2"), q("3"), q("5")],
    qs(["300%", "600%", "1500%", "3000%"], dmg), qs(["8m", "10m", "14m", "20m"]), qs(["1s", "1.5s", "2.5s", "4s"])))

EQUIPMENT["Magnet"] = ("Pulled items may gain quality.", add(per_tier(
    "Each item on the ground it pulls has a {0} chance to become {1} quality. Each item only gets " + util("one roll") + ".",
    qs(["5%", "10%", "25%", "50%"]), qs(["Uncommon", "Rare", "Epic", "Legendary"]))))

EQUIPMENT["MIDAS"] = ("Costs less health, pays more gold, and starts a gold rush.", per_tier(
    "Sacrifice {0} of your " + health("current health") + ", gaining either the amount of " + health("health lost") + " or " + util("the price of a standard chest") + ", times {1}, as " + util("gold") + ", depending on which is higher.\n"
    "For {2} after, every kill pays {3} of the health you sacrificed as " + util("gold") + ".",
    qs(["45%", "40%", "30%", "15%"], health), qs(["1.25", "1.5", "2.25", "3.5"]),
    qs(["4s", "6s", "10s", "15s"]), qs(["10%", "20%", "50%", "100%"])))

EQUIPMENT["WhiteFlag"] = ("A bigger flag that only silences enemies and heals allies.", per_tier(
    "Place a white flag that " + util("disables skill usage") + " of " + q("enemies only") + " within {0}. Lasts {1}. Allies near it regenerate {2} of their " + heal("maximum health") + " per second.",
    qs(["30m", "35m", "50m", "75m"]), qs(["16 seconds", "18 seconds", "22 seconds", "30 seconds"]),
    qs(["2%", "4%", "10%", "20%"], heal)))

# ================================================================ HALLOWED CONCEPTS
# Remove this part if Quality ever covers the DLC itself.
# Keyed by ItemDef.name (Quality builds its tokens from that), not the DLC's own token names.

death = style("cDeath")

ITEMS["BlightZone"] = ("Enemies dying in the cloud feed the Teleporter and burst.", add(per_tier(
    "Enemies that die inside the cloud add {0} " + util("Teleporter charge") + " and " + dmg("burst") + ", inflicting " + dmg("Blight") + " on enemies within {1}.",
    qs(["0.5%", "1%", "2.5%", "5%"]), qs(["3m", "5m", "8m", "12m"]))))

ITEMS["LowHealthOrbSpawner"] = ("Can save an item without being used up, and restores itself.", add(per_tier(
    "When the Crucible saves an item, {0} chance it isn't used up itself. When consumed, it " + util("restores itself") + " after {1}.",
    qs(["10%", "20%", "50%", "90%"]), qs(["10 minutes", "7 minutes", "4 minutes", "2 minutes"]))))

ITEMS["LowHealthOrbSpawnerConsumed"] = ("", [BASE] * 4)

ITEMS["RechargeableJump"] = ("Unused jumps grant stats and shake the ground.", add(per_tier(
    "Each unused jump also grants {0} movement speed, attack speed and damage, {1} crit and {2} regeneration. "
    "While you stand on the ground, each unused jump " + util("slows") + " enemies within {3} by {4}.",
    qs(["+2%", "+4%", "+8%", "+15%"], dmg), qs(["+1%", "+2%", "+4%", "+8%"], dmg), qs(["+0.25", "+0.5", "+1.25", "+2.5"], heal),
    qs(["8m", "10m", "14m", "20m"]), qs(["4%", "8%", "15%", "25%"]))))

ITEMS["CausticPheromones"] = ("Brand sooner; grow stronger with every brand.", per_tier(
    "After hitting an enemy with " + util("skills") + " {0} times, " + dmg("stun") + " and " + dmg("soul-brand") + " that enemy for {1} " + stack("+5s per stack") + ". "
    "Soul-branded enemies " + util("draw attention") + " and " + dmg("have friendly fire enabled") + ". Once soul-branding wears off, the target " + util("cannot be soul-branded again") + " for 15 seconds.\n"
    "Gain {2} " + dmg("damage") + " and " + dmg("attack speed") + " for every enemy you have branded. With {3} enemies branded at once, they are all " + dmg("executed") + " (not bosses). "
    "Branded enemies take {4} damage from their allies.",
    qs(["8", "7", "5", "3"]), qs(["6s", "7s", "9s", "13s"]),
    qs(["+2%", "+4%", "+10%", "+20%"], dmg), qs(["8", "7", "5", "4"]), qs(["+25%", "+50%", "+125%", "+250%"], dmg)))

ITEMS["DreamDemon"] = ("Bigger, stronger demons that jump to a new host.", per_tier(
    dmg("20%") + " chance on hit to haunt an enemy with a {0} " + dmg("Nightmare Demon") + ". Damaging a haunted enemy charges the demon. Upon reaching a charge of " + dmg("1600%") + " damage, hitting the demon with any " + util("skill") + " causes it to " + dmg("explode") + " for {1} " + stack("+600% per stack") + " base damage and activate all your " + dmg("On-kill") + " effects.\n"
    "If its host dies first, {2} chance the demon jumps to a nearby enemy, keeping its charge.",
    qs(["x1.25 size", "x1.5 size", "x2 size", "x3 size"]),
    qs(["750%", "900%", "1500%", "2400%"], dmg), qs(["25%", "40%", "70%", "100%"])))

ITEMS["SprintDamage"] = ("Skulls stun, longer the faster you go.", add(per_tier(
    "Skulls " + util("stun") + " the enemies they hit for {0}, multiplied by how far above sprint speed you are (up to 6s).",
    qs(["0.4s", "0.8s", "2s", "4s"]))))

ITEMS["SanguineChalice"] = ("More bleed, more healing; spare blood becomes a wave.", per_tier(
    "{0} chance to " + dmg("bleed") + " an enemy. Your " + dmg("bleed") + " damage " + heal("heals you") + " for {1} " + stack("+0.5% per stack") + " of your maximum health per tick.\n"
    "Bleed healing you get at full health is stored as " + util("blood") + ", up to {2} of your maximum health. Starting to " + util("sprint") + " releases it as a " + dmg("wave") + " within {3}, dealing {4} of the stored blood as damage and making enemies " + dmg("bleed") + ".",
    qs(["15%", "20%", "35%", "60%"], dmg), qs(["0.6%", "0.7%", "1%", "1.5%"], heal),
    qs(["50%", "75%", "125%", "200%"]), qs(["12m", "15m", "20m", "28m"]), qs(["200%", "400%", "1000%", "2000%"], dmg)))

ITEMS["GhostBlades"] = ("Bigger, deadlier blades.", add(per_tier(
    "Blades are {0} bigger and deal {1} damage. Blade hits have a {2} chance to " + dmg("execute") + " (not bosses).",
    qs(["x1.5", "x2", "x3", "x5"]), qs(["x1.5", "x2", "x3.5", "x6"], dmg), qs(["1%", "2%", "5%", "10%"]))))

ITEMS["GoldenGoose"] = ("Recharges faster; gilded kills pay more.", add(per_tier(
    "Recharges in {0}. Gilded enemies that die pay {1} of their gold reward on top.",
    qs(["9s", "8s", "6s", "4s"]), qs(["20%", "40%", "100%", "200%"]))))

ITEMS["VengefulSpirit"] = ("A huge spirit that explodes when it dies, and comes back.", add(per_tier(
    "The spirit is {0} bigger. When it dies, it explodes in {1}: enemies take {2} base damage and allies " + heal("heal") + " {3} of their maximum health. "
    "It comes back after {4}, and the vengeance it gave you lasts until the end of the stage, so it " + util("builds up") + ".",
    qs(["x1.5", "x2", "x3", "x5"]), qs(["12m", "15m", "20m", "30m"]),
    qs(["400%", "800%", "2000%", "4000%"], dmg), qs(["10%", "15%", "30%", "60%"], heal),
    qs(["12 minutes", "9 minutes", "5 minutes", "2.5 minutes"]))))

ITEMS["VengefulSpiritRageMode"] = ("", [BASE] * 4)

ITEMS["VileCenser"] = ("Tendrils link enemies together.", add(per_tier(
    "Enemies hit by the tendrils are " + util("linked") + " for 1s: damage dealt to one is shared with the others at {0}. "
    "When a linked enemy dies, each other one has a {1} chance to be " + dmg("executed") + " (not bosses).",
    qs(["10%", "20%", "40%", "75%"], dmg), qs(["3%", "6%", "15%", "30%"]))))

# ---- hexes: a quality hex keeps its effect and adds a twist; cleansing it guarantees a quality choice.
CLEANSE = "\n" + Q + " Cleansing it guarantees a choice of its quality."


def hex_line(lines):
    return [line + CLEANSE for line in lines]


ITEMS["SlipperyGround"] = ("A gale sweeps the stage.", hex_line(add(per_tier(
    "A wind sweeps the stage and pushes every player at {0}, turning to a new direction every {1}.",
    qs(["3 m/s", "6 m/s", "12 m/s", "20 m/s"], death), qs(["20s", "15s", "12s", "8s"], death)))))

ITEMS["Blindness"] = ("Phantoms appear around you.", hex_line(add(per_tier(
    "Every {0}, {1} phantom copies of nearby enemies appear around you. They deal no damage and burst into smoke when hit.",
    qs(["12s", "9s", "6s", "4s"], death), qs(["1", "2", "3", "5"], death)))))

ITEMS["LowerHealing"] = ("You get hungry.", hex_line(add(per_tier(
    "A hunger bar empties over {0}. Healing fills it first; while it's empty you starve, losing {1} of your maximum health per second.",
    qs(["90s", "60s", "40s", "25s"], death), qs(["1%", "2%", "4%", "8%"], death)))))

ITEMS["ShareItemsWithEnemies"] = ("Small monsters pick your pockets.", hex_line(add(per_tier(
    "Small, non-elite monsters next to you have {0} chance per second to snatch an item and run away with it, {1} faster. Kill the thief to get it back; if it stays far away for {2}, it escapes with it.",
    qs(["5%", "10%", "25%", "45%"], death), qs(["40%", "70%", "120%", "200%"], death), qs(["8s", "7s", "6s", "5s"], death)))))

ITEMS["HexHaunting"] = ("Wisps carry your loot away.", hex_line(add(per_tier(
    "Items that land near you are grabbed by a wisp {0} and flown away from you{1}. It is immune for a moment, then shoot it or catch it to make it drop the item; it lets go after 20s.",
    ["after " + q("0.6s", death), "after " + q("0.4s", death), "after " + q("0.2s", death), q("instantly", death)],
    [", " + q("75%", death) + " faster", ", " + q("150%", death) + " faster", ", " + q("300%", death) + " faster", ", " + q("600%", death) + " faster"]))))

ITEMS["WeakAssHamstring"] = ("Jumps cost more height and throw you around.", hex_line(per_tier(
    "Jumping " + death("reduces") + " your jump height by {0} and " + death("pushes you sideways") + " in a random direction at {1}. Your jump height " + util("returns gradually") + " over " + util("6s") + " " + stack("+6 per stack") + ". "
    "Additionally, " + death("reduces") + " the amount of distance you can " + death("fall") + " without taking " + death("fall damage") + ".",
    qs(["30%", "35%", "50%", "75%"], death), qs(["5 m/s", "10 m/s", "25 m/s", "50 m/s"], death))))

ITEMS["RoamingItemDisable"] = ("Your items slip out of your hands.", hex_line(add(per_tier(
    "When one of your items triggers, {0} chance that a copy of it slips out and bounces away on the ground.",
    qs(["1%", "2%", "5%", "10%"], death)))))

ITEMS["Mimicry"] = ("Mimics split apart when killed.", hex_line(add(per_tier(
    "A Mimic that dies holding your items bursts into {0} smaller Mimics that share the loot{1}. You get the items back when the last one dies.",
    qs(["2", "2", "3", "3"], death), ["", "", "", ", and they split again"]))))

ITEMS["RandomDebuffOnInteract"] = ("Interactions can fail.", hex_line(add(per_tier(
    "Every interaction has a {0} chance to fail. You pay the cost, and nothing happens.",
    qs(["20%", "35%", "50%", "80%"], death)))))

ITEMS["BarrierEnemiesOnKill"] = ("Dead enemies leave bone spikes.", hex_line(add(per_tier(
    "Enemies you kill leave {0} of bone spikes that block the way and shoot a shard at you every {1} for {2} of their damage, crumbling after {3}.",
    qs(["a small cluster", "a cluster", "a big cluster", "a huge cluster"], death), qs(["2s", "1.5s", "1s", "0.6s"], death),
    qs(["100%", "150%", "250%", "400%"], death), qs(["4s", "6s", "8s", "12s"], death)))))

ITEMS["HexCurse"] = ("Your pain is shared with your allies.", hex_line(add(per_tier(
    "Every hit you take is also dealt at {0} to each ally within {1} (players, drones, turrets, summons). With no ally nearby, {2} of the hit comes back to you.",
    qs(["10%", "25%", "60%", "120%"], death), qs(["15m", "20m", "30m", "40m"], death), qs(["5%", "10%", "25%", "50%"], death)))))

ITEMS["OutOfTPRadius"] = ("A reaper follows you outside the zone.", hex_line(add(per_tier(
    "Outside the charging Teleporter, a reaper swings at you every second, with a {0} chance to " + death("execute") + " you that doubles every {1} you stay out.",
    qs(["1%", "2%", "3%", "5%"], death), qs(["20s", "15s", "8s", "5s"], death)))))

ITEMS["IncreasedDifficulty"] = ("Storm clouds drift over the stage.", hex_line(add(per_tier(
    "{0} red storm {1} around the stage, {2} wide. Enemies under one are enraged: {3} attack speed and damage. Under one, you lose {4} " + util("luck") + ".",
    qs(["1", "2", "3", "4"], death), ["cloud drifts", "clouds drift", "clouds drift", "clouds drift"],
    qs(["16m", "20m", "26m", "32m"], death), qs(["+15%", "+30%", "+60%", "+100%"], death), qs(["1", "1", "2", "3"], death)))))

ITEMS["HexMountain"] = ("Teleporter bosses pass a crown around.", hex_line(add(per_tier(
    "{0} a crown ({1} health, {2} damage). When the wearer dies, the crown jumps to the nearest enemy, up to {3} times.",
    ["The Teleporter boss wears", "The Teleporter boss wears", "The Teleporter boss wears", q("2", death) + " Teleporter bosses wear"],
    qs(["+50%", "+100%", "+200%", "+400%"], death), qs(["+25%", "+50%", "+100%", "+200%"], death), qs(["1", "2", "3", "5"], death)))))

# ---------------------------------------------------------------- buffs (LookingGlass tooltips)

BUFFS = {
    "bdQualitySS2BloodRush": ("Blood Rush", "Broken Blood Tester's heal got you going: faster movement, attack speed, damage and armor."),
    "bdQualitySS2StimArmor": ("Stim: Armor", "X-4 Stimulant boost: +30 armor."),
    "bdQualitySS2StimSpeed": ("Stim: Speed", "X-4 Stimulant boost: +30% movement speed."),
    "bdQualitySS2StimDamage": ("Stim: Damage", "X-4 Stimulant boost: +25% damage."),
    "bdQualitySS2StimCrit": ("Stim: Crit", "X-4 Stimulant boost: +20% critical chance."),
    "bdQualitySS2StimRegen": ("Stim: Regeneration", "X-4 Stimulant boost: +10 health regeneration."),
    "bdQualitySS2FieldAura": ("Accelerated Field", "Field Accelerator's charging zone: +1% attack speed per stack, and faster projectiles."),
    "bdMoreQualityInfernalFervor": ("Infernal Fervor", "Infernal Mark: more damage and attack speed for every enemy you have soul-branded."),
    "bdMoreQualityHexEmpowered": ("Hex-Empowered", "Summoned by a quality Hex: +1% damage and movement speed per stack."),
    "bdMoreQualityPinned": ("Pinned", "Fork: can't move, and takes +1% damage per stack."),
    "bdMoreQualityHunted": ("Hunted", "Hunter's Sigil: takes +1% damage per stack."),
    "bdMoreQualityVoodoo": ("Voodoo Doll", "Needles: 1% per stack of the damage dealt to it is also dealt to every other voodoo doll."),
    "bdMoreQualitySlowed": ("Slowed", "-1% movement speed per stack."),
    "bdMoreQualityOnTheBeat": ("On the Beat", "Watch Metronome: hits now are critical strikes with bonus damage."),
    "bdMoreQualityMolten": ("Molten", "Molten Coin: you set nearby enemies on fire."),
    "bdMoreQualityOvercharged": ("Overcharged", "Universal Charger: more damage for a moment."),
    "bdMoreQualityGoldRush": ("Gold Rush", "M.I.D.A.S.: every kill pays gold."),
    "bdMoreQualityBloodOffering": ("Blood Offering", "Sanguine Chalice: stored blood, 1% of your max health per stack. Start sprinting to release it as a wave."),
    "bdMoreQualityEnraged": ("Enraged", "Under a storm cloud: +1% attack speed and damage per stack."),
    "bdMoreQualityDrenched": ("Drenched", "Under a storm cloud: -1 luck per stack."),
    "bdMoreQualityCrowned": ("Crowned", "Pass the crown: +1% health and +0.5% damage per stack. When it dies, the crown jumps."),
    "bdMoreQualityReaped": ("Reaped", "Hex of Patience: the reaper swings at you every second. Get back to the Teleporter!"),
}


def build(entries, prefix):
    strings = {}
    for name, (pickup, lines) in entries.items():
        assert len(lines) == 4, name
        for tier, line in zip(TIERS, lines):
            assert "{" not in line and "}" not in line, name + ": leftover placeholder"
            key = prefix + "_" + name.upper() + "_" + tier
            strings[key + "_PICKUP"] = BASE + "\n" + Q + " " + pickup if pickup else BASE
            strings[key + "_DESC"] = line
    return strings


def main():
    root = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lang", "en")
    os.makedirs(root, exist_ok=True)
    items = build(ITEMS, "ITEM")
    for buff, (name, desc) in BUFFS.items():
        items["LG_TOKEN_NAME_" + buff] = name
        items["LG_TOKEN_DESCRIPTION_" + buff] = desc
    for file, strings in (("Items.txt", items), ("Equipment.txt", build(EQUIPMENT, "EQUIPMENT"))):
        with open(os.path.join(root, file), "w", encoding="utf-8") as f:
            json.dump({"strings": strings}, f, indent=4, ensure_ascii=False)
        print(file, len(strings), "strings")


if __name__ == "__main__":
    main()
