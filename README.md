# Pokémon Mystery Dungeon: Green Rescue Team

> [!WARNING]
> This is still a work in progress. We're also using this project to test [GBAModernRuntime](https://github.com/Asphaltian/GBAModernRuntime) and [GBARecomp](https://github.com/Asphaltian/GBARecomp), so expect bugs, and expect shit to change without warning!

Green Rescue Team is Pokémon Mystery Dungeon: Red Rescue Team, recompiled to run natively on PC. It's the same game you know, with a few enhancements as well as mod support.

You need your own copy of the game, specifically the **USA/Australia** version.

## How to Play

The first time you launch it, click **Choose a ROM** and pick your ROM. You can also drop it on `pmd_green`, or pass it on the command line:

```
pmd_green pmd_red.gba
```

The game keeps its own copy of the ROM after that, so you only need to do this once.

## Your Data

Saves, settings, mods and screenshots are stored here:

* Windows: `%LOCALAPPDATA%\Green Rescue Team`
* Linux: `~/.config/Green Rescue Team`
* macOS: `~/Library/Application Support/Green Rescue Team`

If you'd rather keep everything next to the game, put an empty file called `portable.txt` in the folder you run it from.

## Building

You will need the .NET 10 SDK and [Slang](https://github.com/shader-slang/slang/releases), with `slangc` available on your PATH.

```
git clone --recurse-submodules https://github.com/Asphaltian/pmd-green
cd pmd-green
```

Put your ROM in the repository root as `pmd_red.gba`, then grab the symbols from [pret/pmd-red](https://github.com/pret/pmd-red):

```
curl -L -o pmd_red.sym https://raw.githubusercontent.com/pret/pmd-red/66d44bd59/pmd_red.sym
```

Now recompile the ROM, then build and run the game:

```
dotnet run --project lib/GBAModernRuntime/GBARecomp/src/GBARecomp -c Release -- us.toml
dotnet run --project src/PMDGreen -c Release
```

## License

Code is licensed under GPL-3.0. Assets are not covered by this license.
