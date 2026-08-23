using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Newtonsoft.Json.Linq;
using persona.Services;
using persona.UI;

namespace persona.Windows;

public class DesignsWindow : Window, IDisposable
{
    private readonly DesignAnalyzer analyzer;

    private Guid? selected;
    private string filter = string.Empty;
    private List<(string Design, string Items)> conflictRows = new();
    private List<(string Item, string Sources)> bleedRows = new();

    public DesignsWindow(DesignAnalyzer analyzer)
        : base("Persona Designs##PersonaDesigns", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.analyzer = analyzer;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(600, 400),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public void Dispose() { }

    public override void OnOpen()
    {
        if (analyzer.Designs.Count == 0)
            analyzer.Refresh();
    }

    public override void Draw()
    {
        if (PersonaUi.IconButton(FontAwesomeIcon.SyncAlt))
        {
            analyzer.Refresh();
            Recompute();
        }
        ImGui.SameLine();
        ImGui.TextDisabled(analyzer.GlamourerOk ? $"{analyzer.Designs.Count} designs" : "Glamourer not connected");
        ImGui.Separator();

        if (!analyzer.GlamourerOk)
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

        var matches = analyzer.Designs
            .Where(d => d.Value.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(d => d.Value, StringComparer.OrdinalIgnoreCase);

        var any = false;
        foreach (var (id, name) in matches)
        {
            any = true;
            var (clicked, changed) = PersonaUi.DesignSelectable(analyzer, id, name, selected == id);
            if (clicked)
                selected = id;
            if (clicked || changed)
                Recompute();
        }

        if (!any)
            ImGui.TextDisabled("No designs match.");
    }

    private void DrawDesignDetail()
    {
        if (selected is not { } id || analyzer.GetRaw(id) is not { } data)
        {
            ImGui.TextDisabled("Select a design to inspect its gear and mod associations.");
            return;
        }

        ImGui.TextColored(Palette.Heading, "Gear");
        ImGui.Separator();
        DrawGearTable(data["Equipment"] as JObject, analyzer.ItemMods(id));

        ImGui.Spacing();

        ImGui.TextColored(Palette.Heading, "Mod Associations");
        ImGui.Separator();
        DrawModTable(data["Mods"] as JArray);

        ImGui.Spacing();

        ImGui.TextColored(Palette.Heading, "Existing Designs with Conflicts");
        ImGui.Separator();
        DrawConflictTable();

        ImGui.Spacing();

        ImGui.TextColored(Palette.Heading, "Bleed Risks");
        ImGui.Separator();
        DrawBleedTable();
    }

    private static void DrawGearTable(JObject? equipment, IReadOnlyDictionary<string, List<(string Dir, string Name)>> itemMods)
    {
        if (equipment is null)
        {
            ImGui.TextDisabled("No equipment data.");
            return;
        }

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

            var itemName = ItemNames.Resolve(itemId.Value<ulong>());
            var mods = itemMods.GetValueOrDefault(itemName);

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

    private void Recompute()
    {
        if (selected is { } id)
        {
            conflictRows = analyzer.Conflicts(id);
            bleedRows = analyzer.BleedRisks(id);
        }
        else
        {
            conflictRows = new List<(string, string)>();
            bleedRows = new List<(string, string)>();
        }
    }
}
