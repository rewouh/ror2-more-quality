# Developing MoreQuality

## Build

```
python tools/fetch_deps.py
dotnet build -c Release
```

- `tools/fetch_deps.py` downloads the mods that aren't on NuGet (Quality, Starstorm2, MoonstormSharedUtils, RoR2BepInExPack) from Thunderstore into `libs/`, at the versions listed in `thunderstore.toml`. Run it again after changing those versions.
- Game, BepInEx and R2API references come from NuGet (`nuget.config` adds the BepInEx feed).
- On this PC, the build also copies the DLL and `lang/` into the ModdedMulitplayer r2modman profile (`plugins/Rewouh-MoreQuality`). Add `-p:SkipDeploy=true` to build without copying. Elsewhere (CI) that step is skipped.
- **AccessCheck:** the NuGet game references are publicized, so the compiler can silently use a member that is private in the real game (for example `CharacterBody.transform`), which then throws at runtime. On this PC every build runs `tools/AccessCheck` against the real game and profile DLLs and fails if the mod uses anything that isn't accessible.

## Text and icon

- `lang/en/*.txt` is generated: edit `tools/make_lang.py`, then run `python tools/make_lang.py`.
  - When quality changes a number Starstorm2 already shows, the whole description is rewritten with the new value in place, marked with Quality's diamond. Those base numbers are copied from Starstorm2's default config, so check them when updating Starstorm2.
  - When quality only adds an effect, the text starts with `[[BASE]]`, which is replaced at display time by Starstorm2's own description (`src/BaseText.cs`).
  - LookingGlass buff names (`LG_TOKEN_NAME_bdQualitySS2*`) are generated there too.
- `icon.png` is drawn by `tools/make_icon.py`.

## How it works

- **Registration** goes through Quality's public `QualityContentManager.LoadContentAsync` API. Quality generates the four variants of every item (defs, icons, names) and counts each variant as a copy of the base item, so Starstorm2's base effects keep working.
- **Load order:** Quality's content load is held back until Starstorm2 has finished loading (`src/Starstorm.cs`); waiting inside Quality's own coroutine deadlocks the loading screen.
- **Quality effects** live in `src/Items/`, one class per item: Harmony patches on Starstorm2's item code, or stat hooks. If one fails to hook (for example after a Starstorm2 update), it logs an error and the others keep working.

## Differences from the design doc

The design doc, `Starstorm2_Quality_Proposals.txt`, is kept in sync with the code: when an effect or a number changes, update it there too. These are the places where the code differs from the original plan:

- **Simple Magnet:** Starstorm2 never reads its "treasure chance" setting, so instead each item on the ground the magnet pulls gets a one-time 5/8/12/20% chance to be upgraded to the magnet's quality.
- **Guarding Amulet:** the barrier is a percentage of the whole hit from behind (the prevented part alone was too small to notice).
- **Coffee Bag:** Espresso triggers when you grab a bean while holding at least 5 beans, rather than at the full cap (which grows with every bag).
- **Low Quality Speakers:** bosses take at most 10% of their max health per landing.
- **Insecticide:** the poison ticks 4 times per second, so the Legendary chance to kill per 3-second poison is about 17%.
- **N'Kota's Heritage:** only items given to players become quality.
- **Empty Diary:** quality diaries are consumed by this mod (Starstorm2 only removes the base item).
- **Hunter's Sigil and Galvanic Core:** the zone buff and "razed" don't record who caused them, so the best quality on the team decides the bonus.
- **Portable Reactor (Legendary):** the doubled chest drop gives two copies of the same item.

## Releasing

1. Bump `Version` in `src/Plugin.cs` and add a section to `CHANGELOG.md`.
2. Commit, then tag and push: `git tag v0.1.1 && git push origin main --tags`.
3. The `Publish` GitHub workflow builds the package and uploads it to Thunderstore. It refuses tags that don't match `Plugin.cs`.

A Thunderstore version can be uploaded only once, so check the package first: the same workflow runs on every push and keeps the zip as a downloadable artifact, or build it locally with `dotnet tcli build --package-version 0.1.1` (output in `build/`).

The upload needs a Thunderstore service-account token, stored as the repository secret `TCLI_AUTH_TOKEN`.
