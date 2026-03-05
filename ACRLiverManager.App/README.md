# ACR Livery Manager (ACRLM)

A Windows desktop app (WPF, .NET 8) for managing **Assetto Corsa Rally** liveries using a safe **stash + swap** workflow so that each car has **one active livery** at a time.

---

## What it manages

### Game location (target)
ACR stores pak assets here:


<GameRoot>\acr\Content\Paks


### What a livery is
A livery is a *complete set* of **three files** with the same base name:

- `.pak`
- `.utoc`
- `.ucas`

Example:

- `my_livery.pak`
- `my_livery.utoc`
- `my_livery.ucas`

If any of the three files are missing, the livery (or variant) is considered **invalid** and will not be shown/installed.

---

## Installer repository (source)

ACRLM expects your installer repository to be structured like this:


InstallerRoot/
carId/
liveryId/
*.pak
*.utoc
*.ucas


### Variants (numbers / no numbers)
Some downloads contain multiple valid 3-file sets in the same `liveryId` folder (e.g. “numbers” and “no numbers”).

ACRLM treats each **unique base filename** that forms a complete `.pak/.utoc/.ucas` trio as a separate **variant**.

---

## How installs work (stash + swap)

ACRLM enforces:

- **Only one active livery per car** in `<GameRoot>\acr\Content\Paks`

When you activate a livery:

1. ACRLM identifies the currently active livery files for that `carId`
2. Those files are **stashed** (renamed) in the same folder with the prefix:
    - `__ACRLM__`
3. The selected variant’s `.pak/.utoc/.ucas` are copied into `Paks`
4. (Recommended) ACRLM records what it did in a local state file so future scans are reliable

### Stashed file naming
Stashed files are stored in `Paks` and prefixed with:


ACRLM


This makes them easy to filter out of the “active” set and easy to restore.

> Tip: include `carId` / `liveryId` / `variantId` in the stash filename so rescans can be deterministic.

---

## Typical workflow

1. Set your **Game Root** folder (the folder that contains `acr\`)
2. Set your **Installer Root** folder (your repository structured as above)
3. Click **Rescan**
4. Pick a car, select a livery variant
5. Click **Activate**
6. To revert, click **Disable/Restore** (restores the previously stashed set)

---

## Safety / guardrails

ACRLM will:

- Refuse to activate a livery variant unless all `.pak/.utoc/.ucas` files exist
- Refuse operations if `<GameRoot>\acr\Content\Paks` does not exist
- Avoid overwriting unknown files unless they are part of the currently active set for that car (best achieved via state tracking)

---

## Notes for pack authors / downloads

Many packs (e.g. from overtake.gg) are “drop-in ready” for the game, but can have inconsistent naming conventions.

Because filenames alone can be ambiguous, ACRLM works best when it maintains a small **state file** recording which files belong to which car/livery/variant when installed.

---

## Development

- WPF (.NET 8)
- MVVM (recommended)
- `ICommand`-based actions (Rescan / Activate / Restore)
- Async file scanning/copying should be `async Task` with proper exception handling

---