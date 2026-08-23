using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using persona.UI;
using GlamourerApiVersion = Glamourer.Api.IpcSubscribers.ApiVersion;
using PenumbraApiVersion = Penumbra.Api.IpcSubscribers.ApiVersion;

namespace persona.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private readonly PenumbraApiVersion penumbraApiVersion;
    private readonly GlamourerApiVersion glamourerApiVersion;

    public MainWindow(Plugin plugin)
        : base("Persona##Main", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.plugin = plugin;
        penumbraApiVersion = new PenumbraApiVersion(Plugin.PluginInterface);
        glamourerApiVersion = new GlamourerApiVersion(Plugin.PluginInterface);
    }

    public void Dispose() { }

    public override void Draw()
    {
        DrawConnectionStatus("Penumbra", IsConnected(() => penumbraApiVersion.Invoke()));
        ImGui.SameLine();
        DrawConnectionStatus("Glamourer", IsConnected(() => glamourerApiVersion.Invoke()));
        ImGui.Separator();

        if (ImGui.Button("Designs"))
            plugin.ToggleDesignsUi();
    }

    private static bool IsConnected(Action probe)
    {
        try
        {
            probe();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void DrawConnectionStatus(string name, bool connected)
    {
        ImGui.TextColored(Palette.Label, name);
        ImGui.SameLine();

        var icon = connected ? FontAwesomeIcon.Check : FontAwesomeIcon.Times;
        var color = connected ? Palette.Connected : Palette.Disconnected;

        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.TextColored(color, icon.ToIconString());
        ImGui.PopFont();
    }
}
