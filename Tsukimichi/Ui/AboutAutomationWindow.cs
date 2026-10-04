using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Tsukimichi.Ui;

/// <summary>
/// About automation (plan v7, 1.18.0, A10; spec-1.18 §A10): six short paragraphs under headings, factual and
/// calm. What Tsukimichi does itself, what the other plugins do, what the rules say (the User Agreement; it never says
/// automation is safe), where players draw the line, good manners, and that Tsukimichi is local only. A small window of
/// its own, reached from Settings › Automation, the Help window's Companion plugins topic, the Set up your road card and
/// Questionable's first-start confirmation; never shown unasked. "Read the User Agreement" opens the official page
/// through the link question.
/// </summary>
public sealed class AboutAutomationWindow : Window
{
    private const string Id = "###TsukimichiAboutAutomation";
    private const float WidthLogical = 520f;
    private const float ParagraphGapLogical = 10f;

    /// <summary>The FINAL FANTASY XIV User Agreement (North America, English), on Square Enix's support site.</summary>
    public const string UserAgreementUrl = "https://support.na.square-enix.com/rule.php?id=5382&tag=users_en";

    private readonly Action<string> openAgreement;
    private Theme.StyleScope nightChrome;

    /// <param name="openAgreement">Asks before opening a URL in the browser (the link question).</param>
    public AboutAutomationWindow(Action<string> openAgreement)
        : base(Strings.AboutAutomationTitle + Id, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoDocking)
    {
        this.openAgreement = openAgreement ?? throw new ArgumentNullException(nameof(openAgreement));
        RespectCloseHotkey = true;
    }

    /// <summary>Opens the card (or brings it to the front).</summary>
    public void Show()
    {
        WindowName = Strings.AboutAutomationTitle + Id;
        IsOpen = true;
        BringToFront();
    }

    public override void PreDraw() => nightChrome = Theme.PushNightWindow();

    public override void PostDraw()
    {
        nightChrome.Dispose();
        nightChrome = default;
    }

    public override void Draw()
    {
        UiMetrics.ApplyFontScale();
        var width = UiMetrics.Px(WidthLogical);
        ImGui.Dummy(new Vector2(width, 0f));
        Paragraph(Strings.AboutAutomationDoesHeading, Strings.AboutAutomationDoesBody, width, first: true);
        Paragraph(Strings.AboutAutomationOthersHeading, Strings.AboutAutomationOthersBody, width);
        Paragraph(Strings.AboutAutomationRulesHeading, Strings.AboutAutomationRulesBody, width);
        Paragraph(Strings.AboutAutomationLineHeading, Strings.AboutAutomationLineBody, width);
        Paragraph(Strings.AboutAutomationMannersHeading, Strings.AboutAutomationMannersBody, width);
        Paragraph(Strings.AboutAutomationLocalHeading, Strings.AboutAutomationLocalBody, width);

        // The two quiet actions at the foot, right-aligned.
        ImGui.Dummy(new Vector2(0f, UiMetrics.Px(ParagraphGapLogical)));
        var style = ImGui.GetStyle();
        var agreement = ImGui.CalcTextSize(Strings.AboutAutomationAgreement).X + (style.FramePadding.X * 2f);
        var close = ImGui.CalcTextSize(Strings.AboutAutomationClose).X + (style.FramePadding.X * 2f);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + MathF.Max(0f, width - agreement - close - style.ItemSpacing.X));
        if (ImGui.Button(Strings.AboutAutomationAgreement))
        {
            openAgreement(UserAgreementUrl);
        }

        ImGui.SameLine();
        if (ImGui.Button(Strings.AboutAutomationClose))
        {
            IsOpen = false;
        }
    }

    /// <summary>One heading in the body tone over its wrapped paragraph in the secondary tone.</summary>
    private static void Paragraph(string heading, string body, float width, bool first = false)
    {
        if (!first)
        {
            ImGui.Dummy(new Vector2(0f, UiMetrics.Px(ParagraphGapLogical) - ImGui.GetStyle().ItemSpacing.Y));
        }

        TextFlow.Wrapped(heading, width, Theme.U32(Theme.Surface.Text));
        TextFlow.Wrapped(body, width, Theme.U32(Theme.Surface.TextSecondary));
    }
}
