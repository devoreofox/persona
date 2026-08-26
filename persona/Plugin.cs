using System;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using persona.Models;
using persona.Services;
using persona.UI;
using persona.Windows;

namespace persona;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;

    private const string CommandName = "/persona";

    private ModSettingService ModSettings { get; init; }

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("persona");
    private DesignAnalyzer Analyzer { get; init; }
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }
    private DesignsWindow DesignsWindow { get; init; }
    private SuggestionsWindow SuggestionsWindow { get; init; }

    public Plugin()
    {
        Palette.Apply(new ThemeColors());

        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        Analyzer = new DesignAnalyzer(Configuration);
        ModSettings = new ModSettingService();

        ConfigWindow = new ConfigWindow(Analyzer, Configuration);
        MainWindow = new MainWindow(this);
        DesignsWindow = new DesignsWindow(Analyzer, ConfigWindow.Toggle);
        SuggestionsWindow = new SuggestionsWindow(Analyzer, ConfigWindow.Toggle);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(DesignsWindow);
        WindowSystem.AddWindow(SuggestionsWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the Persona window. /persona setting enable|disable|toggle <collection> | Mod Name | Setting Name"
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();
        DesignsWindow.Dispose();
        SuggestionsWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        var trimmed = args.Trim();
        var parts = trimmed.Split(' ', 2, StringSplitOptions.TrimEntries);
        if (parts.Length >= 1 && string.Equals(parts[0], "setting", StringComparison.OrdinalIgnoreCase))
        {
            HandleSettingCommand(parts.Length == 2 ? parts[1] : string.Empty);
            return;
        }

        MainWindow.Toggle();
    }

    private void HandleSettingCommand(string rest)
    {
        const string usage = "Usage: /persona setting enable|disable|toggle <collection> | Mod Name | [Group |] Setting Name [-tmp]";

        var work = rest.Trim();

        if (string.Equals(work, "reset", StringComparison.OrdinalIgnoreCase))
        {
            Report(ModSettings.ResetTemporary());
            return;
        }

        var temporary = false;
        if (work.EndsWith("-tmp", StringComparison.OrdinalIgnoreCase) &&
            (work.Length == 4 || char.IsWhiteSpace(work[^5])))
        {
            temporary = true;
            work = work[..^4].TrimEnd();
        }

        var parts = work.Split(' ', 2, StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || !TryParseAction(parts[0], out var action))
        {
            ChatGui.PrintError($"[Persona] {usage}");
            return;
        }

        var fields = parts[1].Split('|');
        if (fields.Length is not (3 or 4))
        {
            ChatGui.PrintError($"[Persona] {usage}");
            return;
        }

        var collection = fields[0].Trim();
        var mod = fields[1].Trim();
        var group = fields.Length == 4 ? fields[2].Trim() : null;
        var setting = fields[^1].Trim();
        Report(ModSettings.Apply(action, collection, mod, group, setting, temporary));
    }

    private void Report((bool Ok, string Message) result)
    {
        if (!result.Ok)
            ChatGui.PrintError($"[Persona] {result.Message}");
        else if (Configuration.ShowSettingMessages)
            ChatGui.Print($"[Persona] {result.Message}");
    }

    private static bool TryParseAction(string text, out SettingAction action)
    {
        switch (text.ToLowerInvariant())
        {
            case "enable": action = SettingAction.Enable; return true;
            case "disable": action = SettingAction.Disable; return true;
            case "toggle": action = SettingAction.Toggle; return true;
            default: action = SettingAction.Toggle; return false;
        }
    }

    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => MainWindow.Toggle();
    public void ToggleDesignsUi() => DesignsWindow.Toggle();
    public void ToggleSuggestionsUi() => SuggestionsWindow.Toggle();
}
