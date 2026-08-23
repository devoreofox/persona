# Persona

Persona reads your Glamourer designs and the Penumbra mods behind them, works out which mod belongs to which item in each outfit, and flags when two outfits fight over the same item.

## Features

- Detects when two outfits use the same item with different Penumbra mods
- Infers each design's item-to-mod associations automatically from your enabled mods
- Resolves overlaps with mod priority, keeping every design self-contained
- Warns on conflicts it can't safely auto-resolve

## Installation

1. Open XIVLauncher settings
2. Navigate to **Dalamud**, then **Custom Plugin Repositories**
3. Add the following URL:
```
https://github.com/devoreofox/persona/raw/refs/heads/main/repo.json
```
4. Open `/xlplugins` in-game and search for **Persona**

## Usage

Type `/persona` to open the plugin. Persona shows your Glamourer designs, the mods associated with each, and any conflicts it finds between outfits that share items.

## License

AGPL-3.0-or-later

## Attribution

Persona is free software under the AGPL-3.0, and attribution is required. If you fork it, redistribute it, or reuse its code, you must keep the existing copyright and license notices in place and credit the original author (Oreo) with a visible link back to this repository (https://github.com/devoreofox/persona). Please do not remove or hide that attribution.
