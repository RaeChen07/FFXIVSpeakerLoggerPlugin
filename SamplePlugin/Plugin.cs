using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System;
using System.IO;
using System.Text;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using SamplePlugin.Windows;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;

namespace SamplePlugin;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;

    private const string CommandName = "/pmycommand";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("SamplePlugin");
    private ConfigWindow ConfigWindow { get; init; }
    internal MainWindow MainWindow { get; init; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        if (string.IsNullOrWhiteSpace(Configuration.OutputCsvPath))
        {
            var def = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "SpeakerLogger", "chat.csv");
            Configuration.OutputCsvPath = def;
        }


        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Toggle SamplePlugin main window"
        });

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;

        // hook chat
        ChatGui.ChatMessage += OnChatMessage;

        Log.Information($"===A cool log message from {PluginInterface.Manifest.Name}===");
    }

    public void Dispose()
    {
        ChatGui.ChatMessage -= OnChatMessage;

        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();
        MainWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args) => ToggleMainUI();
    private void DrawUI() => WindowSystem.Draw();
    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();

    private void OnChatMessage(
        XivChatType type,
        int timestamp,
        ref SeString sender,
        ref SeString message,
        ref bool isHandled)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(Configuration.Target)) return;

            var senderText = (sender.TextValue ?? string.Empty).Trim();
            if (!SenderMatches(senderText, Configuration.Target)) return;

            // Build line
            var (nameOnly, world) = SplitNameWorld(senderText);
            var msg = (message.TextValue ?? string.Empty)
                .Replace("\r", " ")
                .Replace("\n", " ");
            var channel = type.ToString();

            EnsureCsvHeader(Configuration.OutputCsvPath);
            AppendCsv(Configuration.OutputCsvPath,
                $"{Csv(channel)},{Csv(nameOnly)},{Csv(world)},{Csv(msg)}");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to write chat CSV row.");
        }
    }

    private static bool SenderMatches(string sender, string target)
    {
        var (name, world) = SplitNameWorld(sender);
        var (tName, tWorld) = SplitNameWorld(target);

        if (!string.IsNullOrWhiteSpace(tWorld))
            return name.Equals(tName, StringComparison.OrdinalIgnoreCase)
                && world.Equals(tWorld, StringComparison.OrdinalIgnoreCase);

        return name.Equals(tName, StringComparison.OrdinalIgnoreCase);
    }

    private static (string name, string world) SplitNameWorld(string s)
    {
        var parts = s.Split('@', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 ? (parts[0], parts[1]) : (s, "");
    }

    private static void EnsureCsvHeader(string path)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (!File.Exists(path))
            {
                using var sw = new StreamWriter(path, append: false, new UTF8Encoding(false));
                sw.WriteLine("channel,sender,world,message");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to ensure CSV header.");
        }
    }

    private static void AppendCsv(string path, string line)
    {
        using var sw = new StreamWriter(path, append: true, new UTF8Encoding(false));
        sw.WriteLine(line);
    }

    private static string Csv(object? v)
    {
        var s = v?.ToString() ?? "";
        if (s.Contains(',') || s.Contains('"'))
            s = "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}
