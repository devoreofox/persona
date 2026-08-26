# Persona

A Dalamud plugin for FFXIV that reads your Glamourer designs and the Penumbra mods behind them, works out which mod belongs to which item in each outfit, and shows you where two outfits clash so you can keep every design self-contained.

Persona never edits your designs for you. It reads Glamourer and Penumbra over IPC and tells you exactly what to set; you make the changes in Glamourer.

## Features

- Mirrors your Glamourer design folder tree, with a search box
- Shows a design's gear, which enabled mod covers each item, and its mod associations
- Lists other designs that share an applied item with a different mod
- Flags unresolved conflicts, where a design is missing the disable association to guard a shared item, and tells you the item, the mod to disable, and which design uses it
- Highlights designs with unresolved conflicts in the list and tints them red
- Flags bleed risks, where a vanilla item in a design is modded by another design and could bleed through
- Suggests the associations a design needs: enable the mods actually on it (with their priority), disable clashing sibling mods, and the cascade edits other designs need, each checked off against what is already set
- Toggles Penumbra mod option settings from a chat command, permanently or temporarily
- Whitelist mods (globally or per item) and blacklist designs so Persona ignores them
- Jump straight to a mod in Penumbra or a design in Glamourer from the right-click menu

## Installation

1. Open XIVLauncher settings
2. Navigate to **Dalamud**, then **Custom Plugin Repositories**
3. Add the following URL:
```
https://raw.githubusercontent.com/devoreofox/persona/main/repo.json
```
4. Open `/xlplugins` in-game and search for **Persona**

Persona needs [Glamourer](https://github.com/Ottermandias/Glamourer) and [Penumbra](https://github.com/xivdev/Penumbra) installed and running.

## Usage

Type `/persona` to open the main window, which shows whether Glamourer and Penumbra are connected and has buttons for the Designs and Suggestions windows. The cog in any title bar opens the settings.

### Designs

The left side mirrors your Glamourer folder tree; type in the search box to filter across every folder. Designs with unresolved conflicts are listed at the top and tinted red. Select a design to inspect it on the right:

- **Gear** - each equipped slot, the resolved item name, the mod covering it, and whether the slot is applied
- **Mod Associations** - the design's saved mods, their enabled state, and priority
- **Conflicting Designs** - other designs that share an applied item with a different mod, whether or not it is resolved
- **Unresolved Conflicts** - the item, the mod to disable, and the design that uses it, for every clash this design has not guarded against with a disable association
- **Bleed Risks** - vanilla items in this design that another design mods

Right-click a design to **Open in Glamourer** or to **ignore** it (blacklist), so Persona leaves it out of all analysis.

### Suggestions

Pick a design from the dropdown and Persona lists the associations it needs. **This Design** shows the mods to enable (with the priority they currently use) and the sibling mods to disable; **Other Designs** shows the cascade edits other designs need so they stop clashing with this one. Each row is checked off against what is already set, so refresh after applying edits in Glamourer to update the marks.

- Left-click a mod name to copy it to your clipboard
- Right-click a mod to **Open in Penumbra**, or to whitelist it for all slots or a single item

### Mod settings command

Toggle a Penumbra mod's option from chat:

```
/persona setting enable|disable|toggle <collection> | Mod Name | Setting Name
/persona setting enable|disable|toggle <collection> | Mod Name | Group Name | Setting Name
```

Use the four-field form when an option name appears in more than one group. Use `current` (or `self`) as the collection to target your character's active collection. Add `-tmp` to the end to apply the change as a temporary override instead of a permanent one, and clear those with `/persona setting reset`.

### Settings

Open settings with the cog icon in any Persona title bar, or the cog next to Persona in `/xlplugins`. You can toggle whether mod-setting messages are shown in chat, and manage the whitelist: mods ignored everywhere, and mods ignored only on a specific item.

## Notes

- Persona reads your designs; it never writes to them. Apply the changes it suggests in Glamourer yourself
- Only items a design actually applies are considered for conflicts
- Whitelisting a mod, blacklisting a design, or changing settings takes effect immediately; in the Suggestions window, refresh to update the completion marks after editing in Glamourer

## License

AGPL-3.0-or-later

## Attribution

Persona is free software under the AGPL-3.0, and attribution is required. If you fork it, redistribute it, or reuse its code, you must keep the existing copyright and license notices in place and credit the original author (Oreo / devoreofox) with a visible link back to this repository (https://github.com/devoreofox/persona). Please do not remove or hide that attribution.
