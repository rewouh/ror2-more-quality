"""Downloads the mod DLLs MoreQuality compiles against (the ones not on NuGet) into libs/.

Versions are read from thunderstore.toml, so the build uses the same versions the package depends on.

Run:  python tools/fetch_deps.py
"""
import io
import os
import tomllib
import urllib.request
import zipfile

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")

# Thunderstore package -> DLL file name inside its zip
NEEDED = {
    "Goorakh-Quality": "ItemQualities.dll",
    "TeamMoonstorm-Starstorm2": "Starstorm2.dll",
    "TeamMoonstorm-MoonstormSharedUtils": "MSU.Runtime.dll",
    "RiskofThunder-RoR2BepInExPack": "RoR2BepInExPack.dll",
}

# Optional (soft) dependencies: not in thunderstore.toml, so their version is pinned here.
OPTIONAL = {
    "Rune580-Risk_Of_Options": ("2.8.6", "RiskOfOptions.dll"),
}


def main():
    with open(os.path.join(ROOT, "thunderstore.toml"), "rb") as f:
        dependencies = tomllib.load(f)["package"]["dependencies"]
    libs = os.path.join(ROOT, "libs")
    os.makedirs(libs, exist_ok=True)
    wanted = [(package, dependencies[package], dll) for package, dll in NEEDED.items()]
    wanted += [(package, version, dll) for package, (version, dll) in OPTIONAL.items()]
    for package, version, dll in wanted:
        team, name = package.split("-", 1)
        url = "https://thunderstore.io/package/download/{}/{}/{}/".format(team, name, version)
        request = urllib.request.Request(url, headers={"User-Agent": "MoreQuality-build"})   # the default one is refused
        with urllib.request.urlopen(request) as response:
            archive = zipfile.ZipFile(io.BytesIO(response.read()))
        entry = next(n for n in archive.namelist() if n.split("/")[-1] == dll)
        with open(os.path.join(libs, dll), "wb") as f:
            f.write(archive.read(entry))
        print(package, version, "->", dll)


if __name__ == "__main__":
    main()
