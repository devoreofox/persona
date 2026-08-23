using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;
using Newtonsoft.Json.Linq;
using persona.UI;
using GetChangedItems = Penumbra.Api.IpcSubscribers.GetChangedItemAdapterDictionary;
using GetDesignJObject = Glamourer.Api.IpcSubscribers.GetDesignJObject;
using GetDesignList = Glamourer.Api.IpcSubscribers.GetDesignList;

namespace persona.Windows;

public class DesignsWindow : Window, IDisposable
{
    private readonly GetDesignList getDesignList;
    private readonly GetDesignJObject getDesignJObject;
    private readonly GetChangedItems getChangedItems;

    private Dictionary<Guid, string> designs = new();
    private bool glamourerOk;

    private IReadOnlyDictionary<string, IReadOnlyDictionary<string, object?>> changedItems = new Dictionary<string, IReadOnlyDictionary<string, object?>>();
    private Dictionary<Guid, Dictionary<string, List<(string Dir, string Name)>>> equipMods = new();

    private Guid? selected;
    private JObject? selectedData;
    private string filter = string.Empty;
    private List<(string Design, string Items)> conflictRows = new();
    private List<(string Item, string Sources)> bleedRows = new();

    private Dictionary<string, List<(string Design, string Mod)>> itemModIndex = new();

    public DesignsWindow()
        : base("Persona Designs##PersonaDesigns", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(600, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        getDesignList = new GetDesignList(Plugin.PluginInterface);
        getDesignJObject = new GetDesignJObject(Plugin.PluginInterface);
        getChangedItems = new GetChangedItems(Plugin.PluginInterface);
    }

    public void Dispose() { }

    public override void OnOpen()
    {
        if (designs.Count == 0)
            RefreshDesigns();
    }

    public override void Draw()
    {
        if (IconButton(FontAwesomeIcon.SyncAlt))
            RefreshDesigns();
        ImGui.SameLine();
        ImGui.TextDisabled(glamourerOk ? $"{designs.Count} designs" : "Glamourer not connected");
        ImGui.Separator();

        if (!glamourerOk)
        {
            ImGui.TextDisabled("Open Glamourer to read your designs.");
            return;
        }

        var avail = ImGui.GetContentRegionAvail();
        if (ImGui.BeginChild("##designList", new Vector2(220, avail.Y)))
            DrawDesignList();
        ImGui.EndChild();

        ImGui.SameLine();

        if (ImGui.BeginChild("##designDetail", new Vector2(0, avail.Y)))
            DrawDesignDetail();
        ImGui.EndChild();
    }

    private void DrawDesignList()
    {
        ImGui.TextColored(Palette.Heading, "Designs");
        ImGui.Separator();

        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##designFilter", "Search...", ref filter, 100);
        ImGui.Spacing();

        var matches = designs
            .Where(d => d.Value.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(d => d.Value, StringComparer.OrdinalIgnoreCase);

        var any = false;
        foreach (var (id, name) in matches)
        {
            any = true;
            if (ImGui.Selectable(name, selected == id))
                Select(id);
        }

        if (!any)
            ImGui.TextDisabled("No designs match.");
    }

    private void DrawDesignDetail()
    {
        if (selected is null || selectedData is null)
        {
            ImGui.TextDisabled("Select a design to inspect its gear and mod associations.");
            return;
        }

        ImGui.TextColored(Palette.Heading, "Gear");
        ImGui.Separator();
        DrawGearTable(selectedData["Equipment"] as JObject);

        ImGui.Spacing();

        ImGui.TextColored(Palette.Heading, "Mod Associations");
        ImGui.Separator();
        DrawModTable(selectedData["Mods"] as JArray);

        ImGui.Spacing();

        ImGui.TextColored(Palette.Heading, "Existing Designs with Conflicts");
        ImGui.Separator();
        DrawConflictTable();

        ImGui.Spacing();

        ImGui.TextColored(Palette.Heading, "Bleed Risks");
        ImGui.Separator();
        DrawBleedTable();
    }

    private void DrawGearTable(JObject? equipment)
    {
        if (equipment is null)
        {
            ImGui.TextDisabled("No equipment data.");
            return;
        }

        var mine = SelectedEquipMods;

        if (!ImGui.BeginTable("##gear", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
            return;

        ImGui.TableSetupColumn("Slot");
        ImGui.TableSetupColumn("Item");
        ImGui.TableSetupColumn("Mod");
        ImGui.TableSetupColumn("Apply");
        ImGui.TableHeadersRow();

        foreach (var slot in equipment.Properties())
        {
            var itemId = slot.Value["ItemId"];
            if (itemId is null)
                continue;

            var itemName = ResolveItem(itemId.Value<ulong>());
            var mods = mine.GetValueOrDefault(itemName);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text(slot.Name);
            ImGui.TableNextColumn();
            ImGui.Text(itemName);
            ImGui.TableNextColumn();
            ImGui.Text(mods is { Count: > 0 } ? string.Join(", ", mods.Select(m => m.Name)) : "-");
            ImGui.TableNextColumn();
            ImGui.Text((slot.Value["Apply"]?.Value<bool>() ?? false) ? "yes" : "no");
        }

        ImGui.EndTable();
    }

    private static void DrawModTable(JArray? mods)
    {
        if (mods is null || mods.Count == 0)
        {
            ImGui.TextDisabled("No mod associations on this design.");
            return;
        }

        if (!ImGui.BeginTable("##mods", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
            return;

        ImGui.TableSetupColumn("Mod");
        ImGui.TableSetupColumn("Enabled");
        ImGui.TableSetupColumn("Priority");
        ImGui.TableHeadersRow();

        foreach (var mod in mods)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text(mod["Name"]?.Value<string>() ?? "(unnamed)");
            ImGui.TableNextColumn();
            ImGui.Text((mod["Enabled"]?.Value<bool>() ?? false) ? "on" : "off");
            ImGui.TableNextColumn();
            ImGui.Text((mod["Priority"]?.Value<int>() ?? 0).ToString());
        }

        ImGui.EndTable();
    }

    private void DrawConflictTable()
    {
        if (conflictRows.Count == 0)
        {
            ImGui.TextDisabled("No other design conflicts with this one.");
            return;
        }

        if (!ImGui.BeginTable("##conflicts", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
            return;

        ImGui.TableSetupColumn("Design");
        ImGui.TableSetupColumn("Conflicting Item(s)");
        ImGui.TableHeadersRow();

        foreach (var (design, items) in conflictRows)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text(design);
            ImGui.TableNextColumn();
            ImGui.Text(items);
        }

        ImGui.EndTable();
    }

    private void ComputeConflicts()
    {
        conflictRows = new List<(string, string)>();
        if (selected is not { } sid || !equipMods.TryGetValue(sid, out var mine))
            return;

        foreach (var (id, name) in designs)
        {
            if (id == sid || !equipMods.TryGetValue(id, out var other))
                continue;

            var clashes = new List<string>();
            foreach (var (item, myMods) in mine)
            {
                if (myMods.Count == 0 || !other.TryGetValue(item, out var theirMods) || theirMods.Count == 0)
                    continue;

                if (!myMods.Select(m => m.Dir).ToHashSet().SetEquals(theirMods.Select(m => m.Dir)))
                    clashes.Add(item);
            }

            if (clashes.Count > 0)
                conflictRows.Add((name, string.Join(", ", clashes)));
        }

        conflictRows = conflictRows.OrderBy(r => r.Item1, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private void DrawBleedTable()
    {
        if (bleedRows.Count == 0)
        {
            ImGui.TextDisabled("No vanilla items are at risk of mod bleed.");
            return;
        }

        if (!ImGui.BeginTable("##bleed", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
            return;

        ImGui.TableSetupColumn("Vanilla Item");
        ImGui.TableSetupColumn("Could Bleed From");
        ImGui.TableHeadersRow();

        foreach (var (item, sources) in bleedRows)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.Text(item);
            ImGui.TableNextColumn();
            ImGui.Text(sources);
        }

        ImGui.EndTable();
    }

    private void ComputeBleed()
    {
        bleedRows = new List<(string, string)>();
        if (selected is not { } sid || !equipMods.TryGetValue(sid, out var mine))
            return;

        foreach (var (item, mods) in mine)
        {
            if (mods.Count > 0 || !itemModIndex.TryGetValue(item, out var sources))
                continue;

            var summary = string.Join(", ", sources.Select(s => $"{s.Mod} ({s.Design})").Distinct());
            bleedRows.Add((item, summary));
        }

        bleedRows = bleedRows.OrderBy(r => r.Item1, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private void BuildItemModIndex()
    {
        itemModIndex = new Dictionary<string, List<(string, string)>>();
        foreach (var (id, map) in equipMods)
        {
            var designName = designs.GetValueOrDefault(id, id.ToString());
            foreach (var (item, mods) in map)
            {
                foreach (var (_, modName) in mods)
                {
                    if (!itemModIndex.TryGetValue(item, out var list))
                        itemModIndex[item] = list = new List<(string, string)>();
                    list.Add((designName, modName));
                }
            }
        }
    }

    private Dictionary<string, List<(string Dir, string Name)>> SelectedEquipMods =>
        selected is { } id ? equipMods.GetValueOrDefault(id, new()) : new();

    private void RefreshDesigns()
    {
        try
        {
            designs = getDesignList.Invoke();
            glamourerOk = true;

            if (selected is { } id && !designs.ContainsKey(id))
            {
                selected = null;
                selectedData = null;
            }
        }
        catch
        {
            glamourerOk = false;
            designs = new Dictionary<Guid, string>();
            selected = null;
            selectedData = null;
        }

        try
        {
            changedItems = getChangedItems.Invoke();
        }
        catch
        {
            changedItems = new Dictionary<string, IReadOnlyDictionary<string, object?>>();
        }

        BuildEquipMods();
        BuildItemModIndex();
        ComputeConflicts();
        ComputeBleed();
    }

    private void BuildEquipMods()
    {
        equipMods = new Dictionary<Guid, Dictionary<string, List<(string, string)>>>();
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

            if (data is not null)
                equipMods[id] = EquippedItemMods(data);
        }
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
                if (!(mod["Enabled"]?.Value<bool>() ?? false))
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

            var itemName = ResolveItem(itemId.Value<ulong>());
            if (itemName == "None")
                continue;

            result[itemName] = enabled
                .Where(m => changedItems.TryGetValue(m.Dir, out var items) && items.ContainsKey(itemName))
                .ToList();
        }

        return result;
    }

    private void Select(Guid id)
    {
        selected = id;
        try
        {
            selectedData = getDesignJObject.Invoke(id);
        }
        catch
        {
            selectedData = null;
        }

        if (selectedData is not null && !equipMods.ContainsKey(id))
            equipMods[id] = EquippedItemMods(selectedData);

        ComputeConflicts();
        ComputeBleed();
    }

    private static readonly Dictionary<ulong, string> NameCache = new();

    private static string ResolveItem(ulong itemId)
    {
        if (NameCache.TryGetValue(itemId, out var cached))
            return cached;

        return NameCache[itemId] = ResolveItemUncached(itemId);
    }

    private static string ResolveItemUncached(ulong itemId)
    {
        if (itemId == 0)
            return "None";
        if (itemId > uint.MaxValue)
            return $"#{itemId}";
        if (itemId >= 4_000_000_000ul)
            return "None"; // empty-slot sentinel

        var name = Plugin.DataManager.GetExcelSheet<Item>().GetRowOrDefault((uint)itemId)?.Name.ExtractText();
        return string.IsNullOrEmpty(name) ? $"#{itemId}" : name;
    }

    private static bool IconButton(FontAwesomeIcon icon)
    {
        ImGui.PushFont(UiBuilder.IconFont);
        var pressed = ImGui.Button(icon.ToIconString());
        ImGui.PopFont();
        return pressed;
    }
}
