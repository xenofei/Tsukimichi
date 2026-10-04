using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Tsukimichi.Ui;

/// <summary>
/// The spoiler question of "Open on…" (1.8.0): a quest the spoiler shield masks asks "This page may show spoilers. Open
/// anyway?" before its page opens in the browser. A small window of its own rather than a popup, so any menu (the
/// detail pane's "…", a table row, a My blues card, a Moonlit row) can ask without drawing anything after it closes.
/// The question names the quest as the shield prints it, never the page's title.
/// </summary>
public sealed class LinkConfirmWindow : Window
{
    private const string Id = "###TsukimichiLinkConfirm";

    private readonly Action<string> open;
    private string url = string.Empty;
    private string name = string.Empty;
    private string? body;

    /// <param name="open">Opens a URL in the browser (<see cref="GameLinks.OpenUrl"/>).</param>
    public LinkConfirmWindow(Action<string> open)
        : base(Strings.LinksConfirmTitle + Id, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking)
    {
        this.open = open ?? throw new ArgumentNullException(nameof(open));
        RespectCloseHotkey = true;
    }

    /// <summary>Shows the spoiler question for <paramref name="target"/>; <paramref name="questName"/> is the name the shield prints.</summary>
    public void Ask(string target, string questName) => Ask(target, questName, null);

    /// <summary>
    /// Shows the question for <paramref name="target"/> under <paramref name="title"/>; <paramref name="question"/>
    /// replaces the spoiler question for a page that is not a quest's (the User Agreement, 1.18).
    /// </summary>
    public void Ask(string target, string title, string? question)
    {
        url = target ?? string.Empty;
        name = title ?? string.Empty;
        body = question;
        WindowName = Strings.LinksConfirmTitle + Id;
        Position = ImGui.GetMousePos() + new Vector2(8f, 8f);
        PositionCondition = ImGuiCond.Appearing;
        IsOpen = url.Length > 0;
        BringToFront();
    }

    public override void Draw()
    {
        UiMetrics.ApplyFontScale();
        if (name.Length > 0)
        {
            ImGui.TextUnformatted(name);
        }

        ImGui.TextUnformatted(body ?? Strings.LinksConfirmBody);
        ImGui.Spacing();
        if (ImGui.Button(Strings.LinksConfirmOpen))
        {
            open(url);
            IsOpen = false;
        }

        ImGui.SameLine();
        if (ImGui.Button(Strings.LinksConfirmCancel))
        {
            IsOpen = false;
        }
    }

    public override void OnClose()
    {
        url = string.Empty;
        name = string.Empty;
        body = null;
    }
}
