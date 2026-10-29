using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Penumbra.Api.Enums;
using persona.Models;
using GetAllModSettings = Penumbra.Api.IpcSubscribers.GetAllModSettings;
using OpenMainWindow = Penumbra.Api.IpcSubscribers.OpenMainWindow;
using GetChangedItems = Penumbra.Api.IpcSubscribers.GetChangedItemAdapterDictionary;
using GetCollectionForObject = Penumbra.Api.IpcSubscribers.GetCollectionForObject;
using GetModList = Penumbra.Api.IpcSubscribers.GetModList;
using GetDesignJObject = Glamourer.Api.IpcSubscribers.GetDesignJObject;
using GetDesignListExtended = Glamourer.Api.IpcSubscribers.GetDesignListExtended;
using OpenDesign = Glamourer.Api.IpcSubscribers.OpenDesign;
using OpenQuickDesignBar = Glamourer.Api.IpcSubscribers.OpenQuickDesignBar;

namespace persona.Services;

public sealed class DesignAnalyzer
{
    private readonly Configuration config;
    private readonly GetDesignListExtended getDesignListExtended;
    private readonly GetDesignJObject getDesignJObject;
    private readonly GetChangedItems getChangedItems;
    private readonly GetModList getModList;
    private readonly GetAllModSettings getAllModSettings;
    private readonly GetCollectionForObject getCollectionForObject;
    private readonly OpenMainWindow openMainWindow;
    private readonly OpenDesign openDesign;
    private readonly OpenQuickDesignBar openQuickDesignBar;

    private Dictionary<Guid, string> designs = new();
    private Dictionary<Guid, string> designPaths = new();
    private Dictionary<Guid, JObject> raw = new();
    private IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> changedItems = new Dictionary<string, IReadOnlyDictionary<string, object?>>();

    private Dictionary<Guid, Dictionary<string, List<(string Dir, string Name)>>> equipMods = new();
    private Dictionary<string, List<(Guid Design, string Dir, string Name)>> itemSources = new();
    private Dictionary<Guid, HashSet<string>> enabledDirs = new();
    private Dictionary<Guid, HashSet<string>> disabledDirs = new();
    private Dictionary<Guid, Dictionary<string, int>> enabledPriority = new();

    private Dictionary<string, string> modNames = new();
    private HashSet<string> liveEnabled = new();
    private Dictionary<string, int> livePriority = new();
    private HashSet<Guid> conflicted = new();

    public bool GlamourerOk { get; private set; }
    public IReadOnlyDictionary<Guid, string> Designs => designs;
    public IReadOnlyDictionary<Guid, string> DesignPaths => designPaths;

    public DesignAnalyzer(Configuration config)
    {
        this.config = config;
        getDesignListExtended = new GetDesignListExtended(Plugin.PluginInterface);
        getDesignJObject = new GetDesignJObject(Plugin.PluginInterface);
        getChangedItems = new GetChangedItems(Plugin.PluginInterface);
        getModList = new GetModList(Plugin.PluginInterface);
        getAllModSettings = new GetAllModSettings(Plugin.PluginInterface);
        getCollectionForObject = new GetCollectionForObject(Plugin.PluginInterface);
        openMainWindow = new OpenMainWindow(Plugin.PluginInterface);
        openDesign = new OpenDesign(Plugin.PluginInterface);
        openQuickDesignBar = new OpenQuickDesignBar(Plugin.PluginInterface);
    }

    public (bool Ok, string Message) SetQuickDesign(string name, bool? open)
    {
        Dictionary<Guid, (string DisplayName, string FullPath, uint Color, bool ShownInQdb)> list;
        try { list = getDesignListExtended.Invoke(); }
        catch { return (false, "Glamourer is not available."); }

        var id = Guid.Empty;
        if (!string.IsNullOrWhiteSpace(name))
        {
            var matches = list.Where(kv => string.Equals(kv.Value.DisplayName, name, StringComparison.OrdinalIgnoreCase))
                .Select(kv => kv.Key).ToList();
            if (matches.Count == 0)
                return (false, $"No design named '{name}'.");
            if (matches.Count > 1)
                return (false, $"Multiple designs named '{name}'; open it from the UI instead.");
            id = matches[0];
        }

        try { openQuickDesignBar.Invoke(open, id); }
        catch (Exception e) { return (false, $"Glamourer call failed: {e.Message}"); }

        var state = open switch { true => "opened", false => "closed", _ => "toggled" };
        return (true, id == Guid.Empty ? $"Quick design bar {state}." : $"Quick design bar {state} on '{name}'.");
    }

    public IEnumerable<(string Dir, string Name)> WhitelistedMods() =>
        config.WhitelistedMods
            .Select(dir => (dir, modNames.GetValueOrDefault(dir, dir)))
            .OrderBy(m => m.Item2, StringComparer.OrdinalIgnoreCase);

    public IEnumerable<(string Dir, string Name, string Item)> WhitelistedModItems() =>
        config.WhitelistedModItems
            .SelectMany(kv => kv.Value.Select(item => (kv.Key, modNames.GetValueOrDefault(kv.Key, kv.Key), item)))
            .OrderBy(m => m.Item2, StringComparer.OrdinalIgnoreCase)
            .ThenBy(m => m.Item3, StringComparer.OrdinalIgnoreCase);

    public void OpenModInPenumbra(string dir)
    {
        try { openMainWindow.Invoke(TabType.Mods, dir); }
        catch { }
    }

    public void OpenDesignInGlamourer(Guid id)
    {
        try { openDesign.Invoke(id); }
        catch { }
    }

    public bool IsBlacklisted(Guid id) => config.BlacklistedDesigns.Contains(id.ToString());
    public bool HasConflicts(Guid id) => conflicted.Contains(id);

    public bool IsIgnored(string dir, string item) =>
        config.WhitelistedMods.Contains(dir) ||
        (config.WhitelistedModItems.TryGetValue(dir, out var items) && items.Contains(item));

    public void ToggleWhitelist(string dir)
    {
        if (!config.WhitelistedMods.Add(dir))
            config.WhitelistedMods.Remove(dir);
        config.Save();
        Refresh();
    }

    public void ToggleWhitelistItem(string dir, string item)
    {
        if (!config.WhitelistedModItems.TryGetValue(dir, out var items))
            config.WhitelistedModItems[dir] = items = new HashSet<string>();
        if (!items.Add(item))
            items.Remove(item);
        if (items.Count == 0)
            config.WhitelistedModItems.Remove(dir);
        config.Save();
        Refresh();
    }

    public void ToggleBlacklist(Guid id)
    {
        var key = id.ToString();
        if (!config.BlacklistedDesigns.Add(key))
            config.BlacklistedDesigns.Remove(key);
        config.Save();
        Refresh();
    }

    public JObject? GetRaw(Guid id) => raw.GetValueOrDefault(id);

    public IReadOnlyDictionary<string, List<(string Dir, string Name)>> ItemMods(Guid id) =>
        equipMods.GetValueOrDefault(id) ?? new Dictionary<string, List<(string, string)>>();

    private DesignData Snapshot => new(designs, equipMods, itemSources, enabledDirs, disabledDirs,
        enabledPriority, changedItems, liveEnabled, livePriority, modNames, IsBlacklisted, IsIgnored);

    public List<(string Design, string Items)> ConflictingDesigns(Guid id) => SuggestionEngine.ConflictingDesigns(Snapshot, id);

    public List<(string Design, string Item, string Mod)> UnresolvedConflicts(Guid id) => SuggestionEngine.UnresolvedConflicts(Snapshot, id);

    public List<(string Item, string Sources)> BleedRisks(Guid id) => SuggestionEngine.BleedRisks(Snapshot, id);

    public (List<Suggestion> ThisDesign, List<(string Design, List<Suggestion> Rows)> Others) Suggestions(Guid id) =>
        SuggestionEngine.Suggestions(Snapshot, id);

    public void Refresh()
    {
        try
        {
            var ext = getDesignListExtended.Invoke();
            designs = ext.ToDictionary(kv => kv.Key, kv => kv.Value.Item1);
            designPaths = ext.ToDictionary(kv => kv.Key, kv => kv.Value.Item2);
            GlamourerOk = true;
        }
        catch
        {
            GlamourerOk = false;
            designs = new Dictionary<Guid, string>();
            designPaths = new Dictionary<Guid, string>();
        }

        try
        {
            changedItems = getChangedItems.Invoke();
        }
        catch
        {
            changedItems = new Dictionary<string, IReadOnlyDictionary<string, object?>>();
        }

        RefreshLiveState();

        raw = new Dictionary<Guid, JObject>();
        equipMods = new Dictionary<Guid, Dictionary<string, List<(string, string)>>>();
        enabledDirs = new Dictionary<Guid, HashSet<string>>();
        disabledDirs = new Dictionary<Guid, HashSet<string>>();
        enabledPriority = new Dictionary<Guid, Dictionary<string, int>>();

        foreach (var id in designs.Keys)
        {
            JObject? data;
            try
            {
                data = getDesignJObject.Invoke(id);
            }
            catch
            {
                continue;
            }

            if (data is null) continue;

            raw[id] = data;
            equipMods[id] = EquippedItemMods(data);
            (enabledDirs[id], disabledDirs[id], enabledPriority[id]) = ModStates(data);
        }

        BuildItemSources();

        var snapshot = Snapshot;
        conflicted = designs.Keys
            .Where(id => !IsBlacklisted(id) && SuggestionEngine.UnresolvedConflicts(snapshot, id).Count > 0)
            .ToHashSet();
    }

    private void RefreshLiveState()
    {
        try { modNames = getModList.Invoke(); }
        catch { modNames = new Dictionary<string, string>(); }

        liveEnabled = new HashSet<string>();
        livePriority = new Dictionary<string, int>();
        try
        {
            var obj = getCollectionForObject.Invoke(0);
            if (obj.Item1)
            {
                var settings = getAllModSettings.Invoke(obj.Item3.Item1).Item2;
                if (settings is not null)
                {
                    foreach (var (dir, s) in settings)
                    {
                        if (!s.Item1)
                            continue;
                        liveEnabled.Add(dir);
                        livePriority[dir] = s.Item2;
                    }
                }
            }
        }
        catch
        {
            liveEnabled = new HashSet<string>();
            livePriority = new Dictionary<string, int>();
        }
    }

    // Glamourer design mods used to carry "Enabled": bool; newer designs use "State": "Enabled"/"Disabled".
    private static bool ModEnabled(JToken mod) =>
        mod["State"] is JToken state
            ? string.Equals(state.Value<string>(), "Enabled", StringComparison.OrdinalIgnoreCase)
            : mod["Enabled"]?.Value<bool>() ?? false;

    private static (HashSet<string> Enabled, HashSet<string> Disabled, Dictionary<string, int> Priority) ModStates(JObject design)
    {
        var enabled = new HashSet<string>();
        var disabled = new HashSet<string>();
        var priority = new Dictionary<string, int>();
        if (design["Mods"] is JArray mods)
        {
            foreach (var m in mods)
            {
                var dir = m["Directory"]?.Value<string>();
                if (dir is null)
                    continue;
                if (ModEnabled(m))
                {
                    enabled.Add(dir);
                    priority[dir] = m["Priority"]?.Value<int>() ?? 0;
                }
                else
                {
                    disabled.Add(dir);
                }
            }
        }
        return (enabled, disabled, priority);
    }

    private Dictionary<string, List<(string Dir, string Name)>> EquippedItemMods(JObject design)
    {
        var result = new Dictionary<string, List<(string, string)>>();
        if (design["Equipment"] is not JObject equipment)
            return result;

        var enabled = new List<(string Dir, string Name)>();
        if (design["Mods"] is JArray mods)
        {
            foreach (var mod in mods)
            {
                if (!ModEnabled(mod))
                    continue;
                var dir = mod["Directory"]?.Value<string>();
                if (dir is not null)
                    enabled.Add((dir, mod["Name"]?.Value<string>() ?? dir));
            }
        }

        foreach (var slot in equipment.Properties())
        {
            var itemId = slot.Value["ItemId"];
            if (itemId is null)
                continue;

            if (!(slot.Value["Apply"]?.Value<bool>() ?? false))
                continue;

            var itemName = ItemNames.Resolve(itemId.Value<ulong>());
            if (itemName == "None")
                continue;

            result[itemName] = enabled
                .Where(m => !IsIgnored(m.Dir, itemName) && changedItems.TryGetValue(m.Dir, out var items) && items.ContainsKey(itemName))
                .ToList();
        }

        return result;
    }

    private void BuildItemSources()
    {
        itemSources = new Dictionary<string, List<(Guid, string, string)>>();
        foreach (var (id, map) in equipMods)
        {
            if (IsBlacklisted(id))
                continue;
            foreach (var (item, mods) in map)
            {
                foreach (var (dir, name) in mods)
                {
                    if (!itemSources.TryGetValue(item, out var list))
                        itemSources[item] = list = new List<(Guid, string, string)>();
                    list.Add((id, dir, name));
                }
            }
        }
    }
}
