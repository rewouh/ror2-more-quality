"""Generates lang/en/Items.txt and lang/en/Equipment.txt for MoreQuality.

Two kinds of entries, like Quality's own descriptions:
  - Rewritten: quality changes a number Starstorm2 already shows, so the whole description is
    written out with the changed values in place, marked with Quality's diamond sprite.
  - Added (add()): quality only adds a new effect. "[[BASE]]" is replaced at display time with
    Starstorm2's own description (see src/BaseText.cs), followed by the new line.

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
    [q("600%", dmg), q("900%", dmg), q("1400%", dmg), q("2400%", dmg)],
    [util("4m"), util("7m"), util("11m"), util("20m")]))

ITEMS["BloodTester"] = ("Healing also gives you a rush.", add(per_tier(
    "Each heal also grants " + util("Blood Rush") + " for {0} " + stack("+1s per stack") + ": {1}.",
    [q("4s"), q("5s"), q("6s"), q("8s")],
    [q("+20%") + " movement speed",
     q("+30%") + " movement speed, " + q("+20%", dmg) + " attack speed",
     q("+40%") + " movement speed, " + q("+30%", dmg) + " attack speed, " + q("+20%", dmg) + " damage",
     q("+50%") + " movement speed, " + q("+40%", dmg) + " attack speed, " + q("+30%", dmg) + " damage, " + q("+30", heal) + " armor"])))

ITEMS["CoffeeBag"] = ("More beans; beans at 5+ stacks cut your cooldowns.", per_tier(
    "{0} chance on hit to drop a coffee bean that grants " + dmg("8% attack speed") + " and " + util("movement speed") + " for " + util("6 seconds") + ". Maximum cap of " + dmg("40%") + " " + stack("+40% per stack") + ".\n"
    "Grabbing a bean while you have at least " + util("5") + " beans reduces " + util("all skill cooldowns") + " by {1} " + stack("{2} per stack") + ".{3}",
    [q("12%", dmg), q("14%", dmg), q("17%", dmg), q("22%", dmg)],
    [q("0.5s"), q("1s"), q("1.5s"), q("2.5s")],
    ["+0.5s", "+1s", "+1.5s", "+2.5s"],
    ["", "", "", " Beans within " + q("40m") + " fly to you."]))

ITEMS["DetritiveTrematode"] = ("Infestations spread when the host dies.", add(per_tier(
    "An infested enemy that dies spreads the infestation to {0} " + stack("{1} per stack") + " {2} within {3}, at any health.",
    [q("1"), q("2"), q("3"), q("5")], ["+1", "+2", "+3", "+5"],
    ["enemy", "enemies", "enemies", "enemies"], [q("15m"), q("15m"), q("20m"), q("25m")])))

ITEMS["Diary"] = ("Also gives a random quality item when consumed.", add(per_tier(
    "When consumed, also gain a random item of {0} quality.",
    [q("Uncommon"), q("Rare"), q("Epic"), q("Legendary")])))

ITEMS["Fork"] = ("Even more base damage.", per_tier(
    "Increase your " + dmg("base damage") + " by {0} " + stack("{1} per stack") + ".",
    [q("24%", dmg), q("36%", dmg), q("52%", dmg), q("72%", dmg)],
    ["+24%", "+36%", "+52%", "+72%"]))

ITEMS["GuardingAmulet"] = ("Hits from behind also grant barrier.", add(per_tier(
    "Hits from behind also grant " + heal("barrier") + " equal to {0} " + stack("{1} per stack") + " of the hit's damage.{2}",
    [q("10%", heal), q("20%", heal), q("35%", heal), q("50%", heal)],
    ["+10%", "+20%", "+35%", "+50%"],
    ["", "", "", " The attacker is " + q("stunned") + " for 1s."])))

ITEMS["Malice"] = ("The splash can keep chaining.", add(per_tier(
    "When the splash runs out of targets, the last enemy hit has a {0} " + stack("{1} per stack") + " chance to pass it on, up to {2} extra times.",
    [q("15%"), q("25%"), q("40%"), q("60%")], ["+15%", "+25%", "+40%", "+60%"],
    [q("3"), q("5"), q("8"), q("12")])))

ITEMS["MoltenCoin"] = ("Burning enemies pay out when they die.", per_tier(
    "{0} chance to " + dmg("ignite") + " enemies on hit for " + dmg("200%") + " " + stack("+200% per stack") + " TOTAL damage and earn " + util("$1") + " " + stack("+$1 per stack") + ". "
    "Killing a burning enemy earns {1} " + stack("{2} per stack") + ". " + util("Scales over time") + ".",
    [q("8%", dmg), q("10%", dmg), q("13%", dmg), q("18%", dmg)],
    [q("$2"), q("$4"), q("$6"), q("$10")], ["+$2", "+$4", "+$6", "+$10"]))

ITEMS["Needles"] = ("More needles, more often.", per_tier(
    "Gain " + dmg("5%") + " " + stack("+5% per stack") + " " + dmg("critical chance") + ". " + dmg("Critical strikes") + " have a {0} chance to " + dmg("needle") + " an enemy for {1}" + dmg("x10%") + " " + stack("+10% per stack") + " TOTAL damage.{2}",
    [q("30%", dmg), q("40%", dmg), q("55%", dmg), q("75%", dmg)],
    [q("4", dmg), q("5", dmg), q("6", dmg), q("8", dmg)],
    ["", "", "", " Needles make the target " + q("bleed", dmg) + "."]))

ITEMS["UniversalCharger"] = ("Refreshes come back faster.", per_tier(
    "Every {0} " + stack("-10% per stack") + ", instantly " + util("refresh the cooldown") + " of your next {1}.",
    [q("15.7 seconds"), q("13.8 seconds"), q("12.4 seconds"), q("11.3 seconds")],
    [util("non-Primary skill"), util("non-Primary skill"), util("non-Primary skill"), q("non-Primary skills, all at once")]))

ITEMS["UraniumHorseshoe"] = ("Even faster; at high quality, ignore falls.", per_tier(
    "Increases " + util("movement speed") + " and " + util("jump height") + " by {0} " + stack("{1} per stack") + ".{2}",
    [q("30%"), q("45%"), q("65%"), q("90%")], ["+30%", "+45%", "+65%", "+90%"],
    ["", "", " Immune to " + q("fall damage") + ".",
     " Immune to " + q("fall damage") + ". Landing from a fall " + q("stomps", dmg) + " for up to " + q("600%", dmg) + " base damage in 10m and " + util("stuns") + "."]))

ITEMS["X4"] = ("Your secondary can grant a random boost.", add(per_tier(
    "Using your " + util("secondary skill") + " {0} for " + util("5s") + ": armor, movement speed, damage, crit, barrier or regeneration.",
    ["has a " + q("25%") + " " + stack("+10% per stack") + " chance to grant a random boost",
     "has a " + q("40%") + " " + stack("+10% per stack") + " chance to grant a random boost",
     "has a " + q("60%") + " " + stack("+10% per stack") + " chance to grant a random boost",
     "grants " + q("2") + " different random boosts"])))

# ---------------------------------------------------------------- green

ITEMS["CrypticSource"] = ("The burst refunds utility cooldown.", per_tier(
    "Upon ending your sprint, unleash a " + dmg("burst of energy") + " in a {0} " + stack("{1} per stack") + " radius for " + dmg("250%") + " " + stack("+250% per stack") + " base damage. "
    "Each enemy in range (up to 3) reduces your " + util("utility cooldown") + " by {2} " + stack("{3} per stack") + ".",
    [util("15m"), util("15m"), util("15m"), q("30m")], ["+3m", "+3m", "+3m", "+6m"],
    [q("0.5s"), q("1s"), q("1.5s"), q("2.5s")], ["+0.5s", "+1s", "+1.5s", "+2.5s"]))

ITEMS["FieldAccelerator"] = ("More charge per kill; allies in the zone attack faster.", per_tier(
    "After defeating the boss, the radius of the Teleporter is increased by " + util("20%") + " " + stack("+20% per stack") + " and killing enemies grants {0} " + util("teleporter charge") + ". "
    "Allies inside the charging zone gain {1} " + stack("{2} per stack") + " " + dmg("attack speed") + ".",
    [q("+1.5%"), q("+2%"), q("+3%"), q("+4%")],
    [q("+5%", dmg), q("+10%", dmg), q("+15%", dmg), q("+25%", dmg)], ["+5%", "+10%", "+15%", "+25%"]))

ITEMS["HuntersSigil"] = ("A bigger zone that also grants crit.", per_tier(
    "After " + util("standing still") + " for " + util("1") + " second, create a zone that grants " + heal("30") + " " + stack("+15 per stack") + " " + heal("armor") + ", "
    + dmg("30%") + " " + stack("+15% per stack") + " increased " + dmg("damage") + " and {0} " + stack("{1} per stack") + " " + dmg("critical chance") + " to all allies within {2}. The zone lasts until you leave it.",
    [q("+10%", dmg), q("+15%", dmg), q("+25%", dmg), q("+40%", dmg)], ["+10%", "+15%", "+25%", "+40%"],
    [q("10m"), q("12m"), q("15m"), q("20m")]))

ITEMS["JetBoots"] = ("Faster recharge and a bigger blast.", per_tier(
    "Gain {0} that " + dmg("ignites") + " enemies for {1} " + stack("+200% per stack") + " base damage in a " + dmg("7.5m") + " " + stack("+2.5m per stack") + " radius. Recharges after {2} seconds.",
    [util("+1 extra jump"), util("+1 extra jump"), util("+1 extra jump"), q("+2 extra jumps")],
    [q("500%", dmg), q("650%", dmg), q("850%", dmg), q("1200%", dmg)],
    [q("8"), q("6.5"), q("5"), q("3")]))

ITEMS["LightningOnKill"] = ("Kills can release a friendly jellyfish.", add(per_tier(
    "{0} " + stack("{1} per stack") + " chance on kill to release a " + util("friendly jellyfish") + " that drifts to the nearest enemy and explodes for {2} base damage.",
    [q("10%"), q("20%"), q("35%"), q("50%")], ["+10%", "+15%", "+20%", "+25%"],
    [q("300%", dmg), q("500%", dmg), q("800%", dmg), q("1200%", dmg)])))

ITEMS["LowQualitySpeakers"] = ("The burst launches enemies sky high.", add(per_tier(
    "The burst launches enemies {0} into the air; they take {1}" + dmg("fall damage") + " when they land.",
    [q("10m"), q("20m"), q("35m"), q("55m")], ["", "", "", q("double", dmg) + " "])))

ITEMS["StrangeCan"] = ("Intoxicated enemies explode on death.", add(per_tier(
    "Intoxicated enemies " + dmg("explode") + " on death in a radius of " + util("2m") + " {0} per intoxicate stack, applying {1} " + stack("+1 per stack") + " intoxicate to everything hit.",
    [q("+0.5m"), q("+1m"), q("+1.5m"), q("+2.5m")], [q("1"), q("2"), q("3"), q("5")])))

ITEMS["WatchMetronome"] = ("Deal more damage while charged.", add(per_tier(
    "While charged, deal {0} " + stack("{1} per stack") + " " + dmg("damage") + ".",
    [q("+10%", dmg), q("+20%", dmg), q("+35%", dmg), q("+50%", dmg)], ["+10%", "+20%", "+35%", "+50%"])))

# ---------------------------------------------------------------- red

ITEMS["DroidHead"] = ("Drones last longer and keep each other alive.", per_tier(
    "Killing an elite enemy spawns a " + dmg("Security Drone") + " " + stack("+100% base damage per stack") + " for {0} " + stack("+10s per stack") + ", with a {1} " + stack("{2} per stack") + " chance to spawn " + util("a second one") + ". "
    "Each new drone adds {3} to your other drones. Spawned drones " + util("inherit the elite type") + " of the slain elite.",
    [q("30s", dmg), q("40s", dmg), q("55s", dmg), q("80s", dmg)],
    [q("15%"), q("30%"), q("50%"), q("75%")], ["+15%", "+30%", "+50%", "+75%"],
    [q("5s"), q("10s"), q("15s"), q("25s")]))

ITEMS["ErraticGadget"] = ("Lightning can strike anywhere on the stage.", per_tier(
    "Your lightning strikes " + dmg("twice") + ". {0} chance on hit to fire " + dmg("chain lightning") + " for " + dmg("300%") + " TOTAL damage on up to " + dmg("1") + " " + stack("+1 per stack") + " targets. "
    "Each gadget strike has a {1} " + stack("+5% per stack") + " chance to also strike a " + util("random enemy anywhere") + " on the stage.",
    [q("15%", dmg), q("20%", dmg), q("30%", dmg), q("45%", dmg)],
    [q("5%"), q("10%"), q("20%"), q("35%")]))

ITEMS["GalvanicCore"] = ("Razed enemies take more damage and get random debuffs.", per_tier(
    "{0} " + stack("+10% per stack") + " chance on hit to " + util("stun") + ". Stunned enemies apply " + util("razed") + " to enemies within " + util("15m") + " " + stack("+5m per stack") + ", "
    "reducing their " + health("maximum health") + " by " + health("20%") + ", " + dmg("damage") + " by " + dmg("30%") + ", and " + util("movement speed") + " by " + util("80%") + ". "
    "Razed enemies also take {1} " + stack("{2} per stack") + " damage and get {3} for {4}.",
    [q("15%"), q("20%"), q("30%"), q("45%")],
    [q("+10%", dmg), q("+20%", dmg), q("+30%", dmg), q("+50%", dmg)], ["+10%", "+20%", "+30%", "+50%"],
    [q("1") + " random debuff", q("1") + " random debuff", q("2") + " random debuffs", q("3") + " random debuffs"],
    [q("3s"), q("4s"), q("5s"), q("6s")]))

ITEMS["GreenChocolate"] = ("Triggers on smaller hits; stronger buff.", per_tier(
    "When damaged for at least {0} of your max HP, the excess damage is reduced by " + dmg("50%") + ", and also gain a buff that grants {1} base damage and {2} critical chance for " + dmg("6s") + " " + stack("+3s per stack") + ".",
    [q("17%", health), q("15%", health), q("12%", health), q("8%", health)],
    [q("+60%", dmg), q("+75%", dmg), q("+100%", dmg), q("+150%", dmg)],
    [q("+25%", dmg), q("+30%", dmg), q("+40%", dmg), q("+50%", dmg)]))

ITEMS["Insecticide"] = ("Poison can kill outright.", add(per_tier(
    "Each poison tick has a {0} " + stack("{1} per stack") + " chance to " + dmg("instantly kill") + " the enemy. Doesn't work on bosses.",
    [q("0.25%", dmg), q("0.5%", dmg), q("1%", dmg), q("1.5%", dmg)], ["+0.25%", "+0.25%", "+0.5%", "+0.5%"])))

ITEMS["NkotasHeritage"] = ("Gives quality items.", add(per_tier(
    "Items it gives are {0} quality.", [q("Uncommon"), q("Rare"), q("Epic"), q("Legendary")])))

ITEMS["PortableReactor"] = ("Lasts longer; hit harder while invulnerable.", per_tier(
    "Gain " + util("full invulnerability") + " and " + util("100% movement speed") + " for the first {0} " + stack("+40s per stack") + " of a stage, dealing {1} " + stack("{2} per stack") + " " + dmg("damage") + " meanwhile.{3}",
    [q("100s"), q("120s"), q("140s"), q("180s")],
    [q("+20%", dmg), q("+40%", dmg), q("+70%", dmg), q("+100%", dmg)], ["+20%", "+40%", "+70%", "+100%"],
    ["", "", "", " Chests opened while invulnerable drop " + q("2") + " items."]))

ITEMS["SwiftSkateboard"] = ("More stacks, and each one adds attack speed.", per_tier(
    util("Sprint") + " in " + util("any direction") + ". Using a " + util("skill") + " grants a buff which provides " + util("+20%") + " " + stack("+15% per stack") + " " + util("movement speed") + " and {0} " + stack("{1} per stack") + " " + dmg("attack speed") + " for " + util("6") + " seconds, up to {2} stacks.",
    [q("+4%", dmg), q("+6%", dmg), q("+9%", dmg), q("+12%", dmg)], ["+4%", "+6%", "+9%", "+12%"],
    [q("5"), q("6"), q("8"), q("10")]))

# ---------------------------------------------------------------- boss

ITEMS["ShackledLamp"] = ("Fires more often; kills fire again.", per_tier(
    "Every {0} primary uses, fire a haunted projectile for " + dmg("200%") + " " + stack("+200% per stack") + " base damage. Kills have a {1} " + stack("+10% per stack") + " chance to fire " + util("another") + " from the victim at the nearest enemy.",
    [q("4", dmg), q("4", dmg), q("3", dmg), q("2", dmg)],
    [q("15%"), q("25%"), q("40%"), q("60%")]))

# ---------------------------------------------------------------- equipment

EQUIPMENT["BackThruster"] = ("Lasts longer; ram enemies at speed.", per_tier(
    "For {0} seconds, rapidly build up to " + util("200% movement speed") + " when moving in a straight line. At speed, " + dmg("ramming") + " enemies deals {1} base damage{2}.",
    [q("10"), q("12"), q("15"), q("20")],
    [q("300%", dmg), q("500%", dmg), q("800%", dmg), q("1200%", dmg)],
    ["", "", "", " and " + q("stuns")]))

EQUIPMENT["CloakingHeadband"] = ("Your first strike out of cloak is a big crit.", add(per_tier(
    "{0} a guaranteed " + dmg("critical strike") + " dealing {1} damage.",
    ["Your first hit while cloaked is", "Your first hit while cloaked is", "Your first hit while cloaked is",
     "Every hit in the " + q("first second") + " after you break cloak is"],
    [q("+50%", dmg), q("+100%", dmg), q("+200%", dmg), q("+300%", dmg)])))

EQUIPMENT["GreaterWarbanner"] = ("Bigger banners, more of them.", per_tier(
    "Place a greater warbanner {0} which grants " + heal("+50% health regeneration") + ", " + dmg("20% critical strike chance") + ", and " + util("50% skill cooldown reduction") + ". Can place up to {1} at a time.",
    ["with a " + q("20%") + " larger radius", "with a " + q("40%") + " larger radius", "with a " + q("60%") + " larger radius", "with a " + q("100%") + " larger radius"],
    [util("1"), q("2"), q("2"), q("3")]))

EQUIPMENT["Magnet"] = ("Pulled items may gain quality.", add(per_tier(
    "Each item on the ground it pulls has a {0} chance to become {1} quality. Each item only gets " + util("one roll") + ".",
    [q("5%"), q("8%"), q("12%"), q("20%")], [q("Uncommon"), q("Rare"), q("Epic"), q("Legendary")])))

EQUIPMENT["MIDAS"] = ("Costs less health, pays more gold.", per_tier(
    "Sacrifice {0} of your " + health("current health") + ", gaining either the amount of " + health("health lost") + " or " + util("the price of a standard chest") + ", times {1}, as " + util("gold") + ", depending on which is higher.",
    [q("40%", health), q("35%", health), q("25%", health), q("15%", health)],
    [q("1.25"), q("1.5"), q("2"), q("3")]))

EQUIPMENT["WhiteFlag"] = ("A bigger flag that heals allies.", per_tier(
    "Place a white flag that " + util("disables skill usage") + "{0} within {1}. Lasts {2}. Allies near it regenerate {3} of their " + heal("maximum health") + " per second.",
    ["", "", "", " of " + q("enemies only")],
    [q("30m"), q("35m"), q("40m"), q("50m")],
    [util("15 seconds"), util("15 seconds"), util("15 seconds"), q("25 seconds")],
    [q("2%", heal), q("4%", heal), q("7%", heal), q("10%", heal)]))

# ================================================================ HALLOWED CONCEPTS
# Remove this part if Quality ever covers the DLC itself.
# Keyed by ItemDef.name (Quality builds its tokens from that), not the DLC's own token names.

death = style("cDeath")

ITEMS["BlightZone"] = ("Enemies dying in the cloud feed the Teleporter.", add(per_tier(
    "Enemies that die inside the cloud add {0} " + util("Teleporter charge") + ".{1}",
    [q("0.5%"), q("0.75%"), q("1%"), q("1.5%")],
    ["", "", "", " They also " + q("burst", dmg) + ", inflicting " + dmg("Blight") + " on enemies within 8m."])))

ITEMS["LowHealthOrbSpawner"] = ("Can save an item without being used up.", add(per_tier(
    "When the Crucible saves an item, {0} chance it isn't used up itself.{1}",
    [q("15%"), q("25%"), q("35%"), q("50%")],
    ["", "", "", " When consumed, it " + q("restores itself") + " after 3 minutes."])))

ITEMS["LowHealthOrbSpawnerConsumed"] = ("", [BASE] * 4)

ITEMS["RechargeableJump"] = ("Unused jumps also grant stats.", add(per_tier(
    "Each unused jump also grants {0}.",
    [q("+4%") + " movement speed",
     q("+4%") + " movement speed and " + q("+4%", dmg) + " attack speed",
     q("+4%") + " movement speed, " + q("+4%", dmg) + " attack speed and " + q("+4%", dmg) + " damage",
     q("+4%") + " movement speed, " + q("+4%", dmg) + " attack speed, " + q("+4%", dmg) + " damage, " + q("+2%", dmg) + " crit and " + q("+1", heal) + " regeneration"])))

ITEMS["CausticPheromones"] = ("Brand sooner; grow stronger with every brand.", add(per_tier(
    "Brands after {0} hits and lasts {1} longer. Gain {2} " + dmg("damage") + " and " + dmg("attack speed") + " for every enemy you have branded.{3}",
    [q("8"), q("7"), q("6"), q("4")], [q("1s"), q("2s"), q("3s"), q("5s")],
    [q("+3%", dmg), q("+5%", dmg), q("+7%", dmg), q("+10%", dmg)],
    ["", "", " With " + q("5") + " enemies branded at once, they are all " + dmg("executed") + " (not bosses).",
     " With " + q("5") + " enemies branded at once, they are all " + dmg("executed") + " (not bosses). Branded enemies take " + q("double", dmg) + " damage from their allies."])))

ITEMS["DreamDemon"] = ("Demons jump to a new host.", add(per_tier(
    "When a haunted enemy dies before its demon exploded, {0} chance the demon jumps to a nearby enemy, keeping its charge.",
    [q("25%"), q("40%"), q("60%"), q("90%")])))

ITEMS["SprintDamage"] = ("Skulls stun, longer the faster you go.", add(per_tier(
    "Skulls " + util("stun") + " the enemies they hit for {0}, multiplied by how far above sprint speed you are (up to 6s).",
    [q("0.5s"), q("0.8s"), q("1.2s"), q("2s")])))

ITEMS["SanguineChalice"] = ("More bleed, more healing.", add(per_tier(
    "{0} " + dmg("bleed chance") + ". Each bleed tick heals {1} more of your " + heal("maximum health") + ".{2}",
    [q("+5%", dmg), q("+10%", dmg), q("+15%", dmg), q("+25%", dmg)],
    [q("0.1%", heal), q("0.25%", heal), q("0.4%", heal), q("0.7%", heal)],
    ["", "", "", " At full health, the healing becomes " + q("barrier", heal) + "."])))

ITEMS["GhostBlades"] = ("Bigger, deadlier blades.", add(per_tier(
    "Blades are {0} bigger and deal {1} damage.{2}",
    [q("x1.5"), q("x2"), q("x3"), q("x5")], [q("x1.5", dmg), q("x2", dmg), q("x3", dmg), q("x5", dmg)],
    ["", "", "", " Blade hits have a " + q("5%") + " chance to " + dmg("execute") + " (not bosses)."])))

ITEMS["GoldenGoose"] = ("Recharges faster; gilded kills pay more.", add(per_tier(
    "Recharges in {0}. Gilded enemies that die pay {1} of their gold reward on top.",
    [q("9s"), q("8s"), q("6.5s"), q("5s")], [q("20%"), q("40%"), q("70%"), q("120%")])))

ITEMS["VengefulSpirit"] = ("A bigger spirit that explodes when it dies.", add(per_tier(
    "The spirit is {0} bigger. When it dies, it explodes in {1}: enemies take {2} base damage and allies " + heal("heal") + " {3} of their maximum health.",
    [q("x1.25"), q("x1.5"), q("x2"), q("x3")], [q("12m"), q("15m"), q("20m"), q("30m")],
    [q("400%", dmg), q("700%", dmg), q("1100%", dmg), q("1800%", dmg)],
    [q("10%", heal), q("15%", heal), q("25%", heal), q("40%", heal)])))

ITEMS["VengefulSpiritRageMode"] = ("", [BASE] * 4)

ITEMS["VileCenser"] = ("Tendrils link enemies together.", add(per_tier(
    "Enemies hit by the tendrils are " + util("linked") + " for 1s: damage dealt to one is shared with the others at {0}.{1}",
    [q("10%", dmg), q("20%", dmg), q("35%", dmg), q("50%", dmg)],
    ["", "", "", " When a linked enemy dies, each other one has a " + q("15%") + " chance to be " + dmg("executed") + " (not bosses)."])))

# ---- hexes: a quality hex is worse; cleansing it guarantees a quality choice.
CLEANSE = "\n" + Q + " Cleansing it guarantees a choice of its quality."


def hex_line(lines):
    return [line + CLEANSE for line in lines]


ITEMS["SlipperyGround"] = ("Acceleration swings even lower.", hex_line(per_tier(
    "All ground becomes " + death("slippery, hindering movement") + ". Your acceleration is reduced by " + death("40%") + " " + stack("+30% per stack") + ", swinging to {0} every " + util("3 seconds") + ".",
    [q("55%", death), q("65%", death), q("75%", death), q("90%", death)])))

ITEMS["Blindness"] = ("Even less vision.", hex_line(per_tier(
    "Reduces your vision to {0} " + stack("-50% per stack") + ".",
    [q("65m", death), q("40m", death), q("20m", death), q("1m", death)])))

ITEMS["LowerHealing"] = ("Even less healing.", hex_line(per_tier(
    "All " + heal("healing") + " is reduced by {0} " + stack("+40% per stack") + ".",
    [q("45%", death), q("50%", death), q("60%", death), q("99%", death)])))

ITEMS["ShareItemsWithEnemies"] = ("Shares more often.", hex_line(per_tier(
    "Every {0}, " + death("share a copy") + " of " + dmg("1") + " " + stack("+1 per stack") + " random item from your inventory with {1}.",
    [q("13 seconds"), q("11 seconds"), q("9 seconds"), q("6 seconds")],
    [util("3 enemies"), util("3 enemies"), util("3 enemies"), q("5 enemies")])))

ITEMS["HexHaunting"] = ("Stronger Poltergeists.", hex_line(add(per_tier(
    "Poltergeists deal {0} damage and have {1} chance to gain a random boon.{2}",
    [q("+25%", death), q("+50%", death), q("+100%", death), q("+200%", death)],
    [q("25%"), q("50%"), q("75%"), q("100%")],
    ["", "", "", " They are " + q("immune") + " for 5s when they appear."]))))

ITEMS["WeakAssHamstring"] = ("Jumps cost more height.", hex_line(per_tier(
    "{0} Your jump height " + util("returns gradually") + " over " + util("6s") + " " + stack("+6 per stack") + ". Additionally, " + death("reduces") + " the amount of distance you can " + death("fall") + " without taking " + death("fall damage") + ".",
    ["Jumping " + death("reduces") + " your jump height by " + q("30%", death) + ".",
     "Jumping " + death("reduces") + " your jump height by " + q("35%", death) + ".",
     "Jumping " + death("reduces") + " your jump height by " + q("45%", death) + ".",
     q("You can no longer jump.", death)])))

ITEMS["RoamingItemDisable"] = ("More items disabled.", hex_line(per_tier(
    "Every " + util("30 seconds") + ", {0} of the items in your inventory " + stack("+10% per stack") + " become " + death("disabled") + ".",
    [q("15%", death), q("25%", death), q("35%", death), q("50%", death)])))

ITEMS["Mimicry"] = ("Faster, stronger Mimics.", hex_line(add(per_tier(
    "Mimics gain {0} " + dmg("damage") + " and " + util("movement speed") + ".{1}",
    [q("+20%", death), q("+40%", death), q("+70%", death), q("+100%", death)],
    ["", "", "", " They are " + q("immune") + " for 5s when they appear."]))))

ITEMS["RandomDebuffOnInteract"] = ("Debuffs more likely, and more of them.", hex_line(per_tier(
    "Activating an interactable has a {0} chance to give {1} " + stack("+1 per stack") + " " + death("random debuff") + ". Shrines of Chance guarantee a " + death("random debuff") + " on failure and none on success.",
    [q("60%"), q("70%"), q("80%"), q("100%")], [death("1"), death("1"), q("2", death), q("4", death)])))

ITEMS["BarrierEnemiesOnKill"] = ("More barrier for enemies.", hex_line(per_tier(
    "Killing an enemy grants {0} " + stack("+100% per stack") + " of their maximum health as " + heal("temporary barrier") + " to " + death("all enemies") + " within " + dmg("30m") + " " + stack("+15m per stack") + ".{1}",
    [q("125%", heal), q("150%", heal), q("200%", heal), q("300%", heal)],
    ["", "", " They are also " + q("fully healed", heal) + ".",
     " They are also " + q("fully healed", heal) + ", and an elite's death passes its " + q("elite powers", death) + " to them for 20s."])))

ITEMS["HexCurse"] = ("Even less health.", hex_line(per_tier(
    "Reduces your " + heal("maximum health") + " by {0} " + stack("+30% per stack") + ".",
    [q("35%", death), q("40%", death), q("50%", death), q("80%", death)])))

ITEMS["OutOfTPRadius"] = ("Straying is punished harder.", hex_line(add(per_tier(
    "Outside the charging Teleporter you also take {0} of your " + death("maximum health") + " per second, growing " + death("12.5%") + " every 10s.{1}",
    [q("0.5%", death), q("0.75%", death), q("1%", death), q("1.5%", death)],
    ["", "", " Every " + util("3s") + " outside, gain a " + q("random debuff", death) + ".",
     " Every " + util("3s") + " outside, gain a " + q("random debuff", death) + " and have a " + q("10%", death) + " chance to be " + death("executed") + "."]))))

ITEMS["IncreasedDifficulty"] = ("Difficulty rises faster.", hex_line(per_tier(
    "Increases " + death("Difficulty") + " by {0} " + stack("+25% per stack") + ".",
    [q("30%", death), q("35%", death), q("45%", death), q("100%", death)])))

ITEMS["HexMountain"] = ("More Mountain effects.", hex_line(per_tier(
    "The Teleporter receives {0} " + stack("+1 per stack") + " Shrine of the Mountain effects, " + death("without the extra rewards") + ".",
    [death("1") + " (" + q("10%") + " chance of 2)", death("1") + " (" + q("25%") + " chance of 2)",
     death("1") + " (" + q("50%") + " chance of 2)", q("3", death)])))

# ---------------------------------------------------------------- buffs (LookingGlass tooltips)

BUFFS = {
    "bdQualitySS2BloodRush": ("Blood Rush", "Broken Blood Tester's heal got you going: faster movement, and at higher quality also attack speed, damage and armor."),
    "bdQualitySS2StimArmor": ("Stim: Armor", "X-4 Stimulant boost: +30 armor."),
    "bdQualitySS2StimSpeed": ("Stim: Speed", "X-4 Stimulant boost: +30% movement speed."),
    "bdQualitySS2StimDamage": ("Stim: Damage", "X-4 Stimulant boost: +25% damage."),
    "bdQualitySS2StimCrit": ("Stim: Crit", "X-4 Stimulant boost: +20% critical chance."),
    "bdQualitySS2StimRegen": ("Stim: Regeneration", "X-4 Stimulant boost: +10 health regeneration."),
    "bdQualitySS2FieldAura": ("Accelerated Field", "Field Accelerator's charging zone: +1% attack speed per stack."),
    "bdMoreQualityInfernalFervor": ("Infernal Fervor", "Infernal Mark: more damage and attack speed for every enemy you have soul-branded."),
    "bdMoreQualityHexEmpowered": ("Hex-Empowered", "Summoned by a quality Hex: +1% damage and movement speed per stack."),
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
