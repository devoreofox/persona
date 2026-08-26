# Changelog

## v0.0.0.1 - Initial Release

### Added
- Reads your Glamourer designs and Penumbra mods over IPC and works out which mod covers each item in a design
- Designs window mirrors your Glamourer folder tree, with a search box and a live connection status for Glamourer and Penumbra
- Per-design breakdown: gear (slot, item, covering mod, applied state), mod associations, conflicting designs, unresolved conflicts, and bleed risks
- Unresolved conflicts call out the exact item, the mod to disable, and the design that uses it; designs with unresolved conflicts are listed up top and tinted red
- Suggestions window lists the associations a design needs, with priorities, and the cascade edits its siblings need, each checked off against what is already set
- Right-click a design to open it in Glamourer or ignore it; right-click a mod to open it in Penumbra or whitelist it; left-click a mod name to copy it
- Whitelist mods globally or per item, and blacklist designs, so Persona leaves them out of its analysis
- Toggle Penumbra mod option settings from chat with `/persona setting enable|disable|toggle`, permanently or temporarily with `-tmp`, and clear temporary settings with `/persona setting reset`
- Settings window with a whitelist manager and a toggle for chat messages
