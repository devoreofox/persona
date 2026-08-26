using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using persona.Services;
using persona.UI;

namespace persona.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly DesignAnalyzer analyzer;
    private readonly Configuration config;

    public ConfigWindow(DesignAnalyzer analyzer, Configuration config) : base("Persona Configuration###persona")
    {
        this.analyzer = analyzer;
        this.config = config;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 300),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public void Dispose() { }

    public override void Draw()
    {
        var showMessages = config.ShowSettingMessages;
        if (ImGui.Checkbox("Show mod-setting messages in chat", ref showMessages))
        {
            config.ShowSettingMessages = showMessages;
            config.Save();
        }
        ImGui.Spacing();

        ImGui.TextColored(Palette.Heading, "Whitelisted Mods");
        ImGui.Separator();
        ImGui.TextDisabled("Whitelisted mods are ignored in all conflict, bleed, and suggestion checks.");
        ImGui.Spacing();

        var whitelisted = analyzer.WhitelistedMods().ToList();
        var perItem = analyzer.WhitelistedModItems().ToList();

        if (whitelisted.Count == 0 && perItem.Count == 0)
        {
            ImGui.TextDisabled("No mods whitelisted. Right-click a mod in the Suggestions window to whitelist it.");
            return;
        }

        foreach (var (dir, name) in whitelisted)
        {
            ImGui.PushID($"m{dir}");
            if (PersonaUi.IconButton(FontAwesomeIcon.Trash))
                analyzer.ToggleWhitelist(dir);
            ImGui.SameLine();
            ImGui.Text(name);
            ImGui.PopID();
        }

        if (perItem.Count == 0)
            return;

        ImGui.Spacing();
        ImGui.TextColored(Palette.Heading, "Per-Slot");
        ImGui.Separator();

        foreach (var (dir, name, item) in perItem)
        {
            ImGui.PushID($"i{dir}{item}");
            if (PersonaUi.IconButton(FontAwesomeIcon.Trash))
                analyzer.ToggleWhitelistItem(dir, item);
            ImGui.SameLine();
            ImGui.Text($"{name} on {item}");
            ImGui.PopID();
        }
    }
}
