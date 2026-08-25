using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using persona.Models;
using persona.Services;
using persona.UI;

namespace persona.Windows;

public class SuggestionsWindow : Window, IDisposable
{
    private readonly DesignAnalyzer analyzer;

    private Guid? selected;
    private string filter = string.Empty;
    private bool pendingRecompute;
    private string? copiedDir;
    private DateTime copiedAt;
    private List<Suggestion> thisDesign = new();
    private List<(string Design, List<Suggestion> Rows)> others = new();

    public SuggestionsWindow(DesignAnalyzer analyzer)
        : base("Persona Suggestions##PersonaSuggestions", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.analyzer = analyzer;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(500, 400),
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
        ImGui.TextDisabled("Refresh after applying edits in Glamourer to update the checkmarks.");
        ImGui.Separator();

        if (!analyzer.GlamourerOk)
        {
            ImGui.TextDisabled("Open Glamourer to read your designs.");
            return;
        }

        pendingRecompute = false;
        DrawPicker();
        ImGui.Separator();

        if (selected is null)
        {
            ImGui.TextDisabled("Pick a design to see the associations it needs.");
            return;
        }

        if (ImGui.BeginChild("##suggestionBody"))
        {
            ImGui.TextColored(Palette.Heading, "This Design");
            ImGui.Separator();
            DrawSuggestionTable("##thisDesign", thisDesign);

            ImGui.Spacing();

            ImGui.TextColored(Palette.Heading, "Other Designs");
            ImGui.Separator();
            if (others.Count == 0)
            {
                ImGui.TextDisabled("No other designs need changes.");
            }
            else
            {
                foreach (var (design, rows) in others)
                {
                    ImGui.TextColored(Palette.Label, design);
                    DrawSuggestionTable($"##other_{design}", rows);
                    ImGui.Spacing();
                }
            }
        }
        ImGui.EndChild();

        if (pendingRecompute)
            Recompute();
    }

    private void DrawPicker()
    {
        var current = selected is { } id ? analyzer.Designs.GetValueOrDefault(id, "?") : "Select a design...";
        ImGui.SetNextItemWidth(-1);
        if (!ImGui.BeginCombo("##designPick", current))
            return;

        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##pickFilter", "Search...", ref filter, 100);

        foreach (var (did, name) in analyzer.Designs
                     .Where(d => d.Value.Contains(filter, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(d => d.Value, StringComparer.OrdinalIgnoreCase))
        {
            var (clicked, changed) = PersonaUi.DesignSelectable(analyzer, did, name, selected == did);
            if (clicked)
                selected = did;
            if (clicked || changed)
                Recompute();
        }

        ImGui.EndCombo();
    }

    private void DrawSuggestionTable(string id, List<Suggestion> rows)
    {
        if (rows.Count == 0)
        {
            ImGui.TextDisabled("Nothing to change.");
            return;
        }

        if (!ImGui.BeginTable(id, 5, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
            return;

        ImGui.TableSetupColumn("##done", ImGuiTableColumnFlags.WidthFixed, 24);
        ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthFixed, 80);
        ImGui.TableSetupColumn("Mod");
        ImGui.TableSetupColumn("Priority", ImGuiTableColumnFlags.WidthFixed, 80);
        ImGui.TableSetupColumn("On Item(s)");
        ImGui.TableHeadersRow();

        foreach (var row in rows)
        {
            ImGui.PushID(id + row.ModDir);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            PersonaUi.Icon(row.Done ? FontAwesomeIcon.Check : FontAwesomeIcon.Circle, row.Done ? Palette.Connected : Palette.Muted);

            ImGui.TableNextColumn();
            ImGui.TextColored(row.Enable ? Palette.Connected : Palette.Disconnected, row.Enable ? "Enabled" : "Disabled");

            ImGui.TableNextColumn();
            DrawModCell(row);

            ImGui.TableNextColumn();
            var priority = row.Enable ? row.Priority.ToString() : "-";
            var offset = (ImGui.GetColumnWidth() - ImGui.CalcTextSize(priority).X) * 0.5f;
            if (offset > 0)
                ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
            ImGui.Text(priority);

            ImGui.TableNextColumn();
            ImGui.Text(string.Join(", ", row.Items));

            ImGui.PopID();
        }

        ImGui.EndTable();
    }

    private void DrawModCell(Suggestion row)
    {
        if (ImGui.Selectable(row.ModName))
        {
            ImGui.SetClipboardText(row.ModName);
            copiedDir = row.ModDir;
            copiedAt = DateTime.UtcNow;
        }
        if (ImGui.IsItemHovered())
        {
            var justCopied = copiedDir == row.ModDir && (DateTime.UtcNow - copiedAt).TotalSeconds < 1.5;
            ImGui.SetTooltip(justCopied ? "Copied!" : "Left-click: copy name / Right-click: whitelist");
        }
        if (ImGui.BeginPopupContextItem("##wl"))
        {
            if (ImGui.MenuItem("Open in Penumbra"))
                analyzer.OpenModInPenumbra(row.ModDir);
            if (ImGui.MenuItem("Whitelist mod (all slots)"))
            {
                analyzer.ToggleWhitelist(row.ModDir);
                pendingRecompute = true;
            }
            if (row.Items.Count == 1)
            {
                if (ImGui.MenuItem($"Whitelist on {row.Items[0]}"))
                {
                    analyzer.ToggleWhitelistItem(row.ModDir, row.Items[0]);
                    pendingRecompute = true;
                }
            }
            else if (ImGui.BeginMenu("Whitelist on slot"))
            {
                foreach (var item in row.Items)
                {
                    if (ImGui.MenuItem(item))
                    {
                        analyzer.ToggleWhitelistItem(row.ModDir, item);
                        pendingRecompute = true;
                    }
                }
                ImGui.EndMenu();
            }
            ImGui.EndPopup();
        }
    }

    private void Recompute()
    {
        if (selected is { } id)
        {
            (thisDesign, others) = analyzer.Suggestions(id);
        }
        else
        {
            thisDesign = new List<Suggestion>();
            others = new List<(string, List<Suggestion>)>();
        }
    }
}
