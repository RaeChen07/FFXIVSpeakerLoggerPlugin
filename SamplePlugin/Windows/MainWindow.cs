using System;
using System.IO;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;

namespace SamplePlugin.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin Plugin;

    // UI buffers
    private string _targetBuf = "";
    private string _pathBuf = "";

    // We give this window a hidden ID using ##.
    public MainWindow(Plugin plugin)
        : base("Main##With a hidden ID", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        Plugin = plugin;

        // Seed UI fields from config
        _targetBuf = Plugin.Configuration.Target ?? "";
        _pathBuf = Plugin.Configuration.OutputCsvPath ?? "";
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.TextUnformatted("Log chat lines from a specific speaker to CSV.");
        ImGui.TextUnformatted($"Config flag example: {Plugin.Configuration.SomePropertyToBeSavedAndWithADefault}");
        if (ImGui.Button("Show Settings"))
            Plugin.ToggleConfigUI();

        ImGui.Spacing();
        using (var child = ImRaii.Child("MainContentWithScroll", Vector2.Zero, true))
        {
            if (!child.Success) return;

            // --- Speaker Logger UI ---
            ImGui.TextUnformatted("Speaker Logger");
            ImGui.Separator();

            ImGui.InputText("Target (Name or Name@World)", ref _targetBuf, 64);
            ImGui.InputText("Output CSV Path", ref _pathBuf, 260);

            if (ImGui.Button("Save Settings"))
            {
                Plugin.Configuration.Target = _targetBuf.Trim();
                Plugin.Configuration.OutputCsvPath = _pathBuf.Trim();
                Plugin.Configuration.Save();
            }
            ImGui.SameLine();
            if (ImGui.Button("Open File Location"))
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(Plugin.Configuration.OutputCsvPath))
                    {
                        // Ensure directory exists if user hasn’t saved/created it yet
                        var dir = Path.GetDirectoryName(Plugin.Configuration.OutputCsvPath);
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                            Directory.CreateDirectory(dir);

                        System.Diagnostics.Process.Start("explorer", $"/select,\"{Plugin.Configuration.OutputCsvPath}\"");
                    }
                }
                catch { /* swallow */ }
            }

            ImGuiHelpers.ScaledDummy(10f);
            ImGui.TextDisabled("CSV columns: channel, sender, world, message");
            ImGui.TextDisabled("Tip: Use full Name@World for cross-world accuracy.");

            ImGuiHelpers.ScaledDummy(20f);

            // --- Original sample info ---
            var localPlayer = Plugin.ClientState.LocalPlayer;
            if (localPlayer == null)
            {
                ImGui.TextUnformatted("Our local player is currently not loaded.");
                return;
            }

            if (!localPlayer.ClassJob.IsValid)
            {
                ImGui.TextUnformatted("Our current job is currently not valid.");
                return;
            }

            ImGui.TextUnformatted($"Our current job is ({localPlayer.ClassJob.RowId}) \"{localPlayer.ClassJob.Value.Abbreviation}\"");

            var territoryId = Plugin.ClientState.TerritoryType;
            if (Plugin.DataManager.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var territoryRow))
            {
                ImGui.TextUnformatted($"We are currently in ({territoryId}) \"{territoryRow.PlaceName.Value.Name}\"");
            }
            else
            {
                ImGui.TextUnformatted("Invalid territory.");
            }
        }
    }

    public void Toggle()
    {
        this.IsOpen = !this.IsOpen;
        if (this.IsOpen)
        {
            // Refresh UI buffers from config when reopened
            _targetBuf = Plugin.Configuration.Target ?? "";
            _pathBuf = Plugin.Configuration.OutputCsvPath ?? "";
        }
    }
}
