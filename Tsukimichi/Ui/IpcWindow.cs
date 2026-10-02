using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Tsukimichi.Core.Ipc;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// Developer window for Tsukimichi's own IPC (1.8.0), opened with <c>/tsuki ipc</c> and not listed in <c>/tsuki help</c>:
/// every gate and message (<see cref="IpcChannels.All"/>) with its signature, the release that added it and the
/// subscriber count its provider reports, and a test-call box that calls a gate through Dalamud's own subscriber, as
/// another plugin would (<see cref="IpcProvider.TestCall"/>). For plugin authors and bug reports; English only, like
/// the glyph sheet.
/// </summary>
public sealed class IpcWindow : Window
{
    private static readonly string[] Callable = Array.ConvertAll(Array.FindAll([.. IpcChannels.All], static g => !g.IsMessage), static g => g.Name);

    private readonly IpcProvider provider;
    private int selected;
    private string arguments = string.Empty;
    private string answer = string.Empty;

    public IpcWindow(IpcProvider provider)
        : base("Tsukimichi IPC###TsukimichiIpc")
    {
        this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        Size = new Vector2(720f, 560f);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(420f, 300f) };
    }

    public override void Draw()
    {
        ImGui.TextUnformatted($"API version {IpcChannels.ApiVersion} · {IpcChannels.All.Count} gates and messages · docs/ipc.md");
        DrawTestCall();
        ImGui.Separator();
        DrawGates();
    }

    private void DrawTestCall()
    {
        ImGui.SetNextItemWidth(260f * ImGuiHelpers.GlobalScale);
        using (var combo = ImRaii.Combo("##gate", Callable[selected]))
        {
            if (combo)
            {
                for (var i = 0; i < Callable.Length; i++)
                {
                    if (ImGui.Selectable(Callable[i], i == selected))
                    {
                        selected = i;
                        answer = string.Empty;
                    }
                }
            }
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(220f * ImGuiHelpers.GlobalScale);
        var enter = ImGui.InputTextWithHint("##arguments", "arguments: 66236, or 132 true", ref arguments, 256, ImGuiInputTextFlags.EnterReturnsTrue);
        ImGui.SameLine();
        if (ImGui.Button("Call") || enter)
        {
            answer = provider.TestCall(Callable[selected], arguments);
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Calls the gate through Dalamud's IPC, as another plugin would. PinQuest and OpenQuest act for real.");
        }

        if (answer.Length > 0)
        {
            ImGui.TextWrapped(answer);
        }
    }

    private void DrawGates()
    {
        using var table = ImRaii.Table("##gates", 4, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp);
        if (!table)
        {
            return;
        }

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("Gate", ImGuiTableColumnFlags.WidthStretch, 0.35f);
        ImGui.TableSetupColumn("Signature", ImGuiTableColumnFlags.WidthStretch, 0.45f);
        ImGui.TableSetupColumn("Since", ImGuiTableColumnFlags.WidthStretch, 0.08f);
        ImGui.TableSetupColumn("Subscribers", ImGuiTableColumnFlags.WidthStretch, 0.12f);
        ImGui.TableHeadersRow();
        foreach (var gate in IpcChannels.All)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(gate.Name);
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(gate.Signature);
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(gate.Since);
            ImGui.TableNextColumn();
            var count = provider.SubscriptionCount(gate.Name);
            ImGui.TextUnformatted(count < 0 ? "—" : count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}
