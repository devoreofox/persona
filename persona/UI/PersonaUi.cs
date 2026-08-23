using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using persona.Services;

namespace persona.UI;

public static class PersonaUi
{
    public static bool IconButton(FontAwesomeIcon icon)
    {
        ImGui.PushFont(UiBuilder.IconFont);
        var pressed = ImGui.Button(icon.ToIconString());
        ImGui.PopFont();
        return pressed;
    }

    public static void Icon(FontAwesomeIcon icon, Vector4 color)
    {
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.TextColored(color, icon.ToIconString());
        ImGui.PopFont();
    }

    public static (bool Clicked, bool Changed) DesignSelectable(DesignAnalyzer analyzer, Guid id, string name, bool selected)
    {
        var blacklisted = analyzer.IsBlacklisted(id);
        var changed = false;

        ImGui.PushID(id.ToString());
        var clicked = ImGui.Selectable(blacklisted ? $"{name} (ignored)" : name, selected);
        if (ImGui.BeginPopupContextItem("##bl"))
        {
            if (ImGui.MenuItem(blacklisted ? "Stop ignoring this design" : "Ignore this design (blacklist)"))
            {
                analyzer.ToggleBlacklist(id);
                changed = true;
            }
            ImGui.EndPopup();
        }
        ImGui.PopID();

        return (clicked, changed);
    }
}
