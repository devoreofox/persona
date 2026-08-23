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

    private const string CommandName = "/persona";

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

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);
        DesignsWindow = new DesignsWindow(Analyzer);
        SuggestionsWindow = new SuggestionsWindow(Analyzer);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(DesignsWindow);
        WindowSystem.AddWindow(SuggestionsWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the Persona window."
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
        MainWindow.Toggle();
    }

    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => MainWindow.Toggle();
    public void ToggleDesignsUi() => DesignsWindow.Toggle();
    public void ToggleSuggestionsUi() => SuggestionsWindow.Toggle();
}
