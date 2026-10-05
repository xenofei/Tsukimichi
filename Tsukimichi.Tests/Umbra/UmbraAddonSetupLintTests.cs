using System.Text.RegularExpressions;
using Tsukimichi.Tests.Diagnostics;
using Tsukimichi.Tests.Localization;

namespace Tsukimichi.Tests.Umbra;

/// <summary>
/// Source lint for setting up Tsukimichi for Umbra (the logic is tested in Core, <see cref="UmbraAddonSetupTests"/> and
/// <see cref="UmbraSetupRunnerTests"/>): nothing writes Umbra's files, Umbra changes only from the card's "Agree and add"
/// (and undoes only from Settings' Remove confirmation), the card answers to no key and stays answered, and no network
/// code came with it. The test project references Core and GameData only, so it reads the plugin's sources.
/// </summary>
public sealed class UmbraAddonSetupLintTests
{
    /// <summary>The files this feature added or touched, plugin and Core.</summary>
    private static readonly string[] SetupFiles =
    [
        Path.Combine("Tsukimichi", "Game", "UmbraControl.cs"),
        Path.Combine("Tsukimichi", "Game", "UmbraAddonSetupService.cs"),
        Path.Combine("Tsukimichi", "Ui", "UmbraAddonCard.cs"),
        Path.Combine("Tsukimichi", "Ui", "ConfigWindow.UmbraAddonSetup.cs"),
        Path.Combine("Tsukimichi", "Ui", "ConfigWindow.Umbra.cs"),
        Path.Combine("Tsukimichi", "Ui", "Strings.UmbraAddonSetup.cs"),
        Path.Combine("Tsukimichi", "Plugin.UmbraAddonSetup.cs"),
        Path.Combine("Tsukimichi", "Config", "Configuration.UmbraAddonSetup.cs"),
        Path.Combine("Tsukimichi.Core", "Umbra", "UmbraAddonSetup.cs"),
        Path.Combine("Tsukimichi.Core", "Umbra", "UmbraSetupRunner.cs"),
        Path.Combine("Tsukimichi.Core", "Umbra", "UmbraSetupCoordinator.cs"),
    ];

    // An API that writes, creates, moves or deletes a file or folder.
    private static readonly Regex WritesFiles = new(
        @"\bFile\.(Write\w*|Append\w*|Create\w*|Delete|Move|Copy|Replace|SetLastWriteTime\w*|Open(?!Read)\w*)\b|FileAccess\.(Write|ReadWrite)\b|FileMode\.(Create|CreateNew|Append|Truncate|OpenOrCreate)\b|\bStreamWriter\b|\bDirectory\.(Delete|CreateDirectory|Move)\b|\bCreateSubdirectory\b|\.(MoveTo|CopyTo)\(",
        RegexOptions.Compiled);

    private static readonly Regex Address = new(@"https?://[^\s""']*", RegexOptions.Compiled);

    private static string Root => ResxFiles.RepositoryRoot();

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));

    /// <summary>The source with <c>//</c> comments (and doc comments) blanked, line by line; good enough for these files, whose strings hold no <c>//</c> but one URL.</summary>
    private static string Code(string relative) =>
        string.Join('\n', Read(relative).Split('\n').Select(static line =>
        {
            var at = line.IndexOf("//", StringComparison.Ordinal);
            return at < 0 || line.Contains("https://", StringComparison.Ordinal) ? line : line[..at];
        }));

    /// <summary>The body of the first member whose declaration contains <paramref name="signature"/>, to its closing brace at the same indent.</summary>
    private static string Member(string source, string signature)
    {
        var at = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(at >= 0, $"not found: {signature}");
        var end = source.IndexOf("\n    }", at, StringComparison.Ordinal);
        return source[at..(end < 0 ? source.Length : end)];
    }

    /// <summary>Every shipped source file of the plugin, relative to the repository.</summary>
    private static IEnumerable<string> PluginFiles()
    {
        var sep = Path.DirectorySeparatorChar;
        return Directory.GetFiles(Path.Combine(Root, "Tsukimichi"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{sep}obj{sep}", StringComparison.Ordinal) && !file.Contains($"{sep}bin{sep}", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(Root, file));
    }

    /// <summary>The plugin files whose code (not comments) contains <paramref name="needle"/>.</summary>
    private static List<string> FilesWith(string needle) =>
        [.. PluginFiles().Where(file => Code(file).Contains(needle, StringComparison.Ordinal)).Order(StringComparer.Ordinal)];

    [Fact]
    public void The_files_exist()
    {
        foreach (var file in SetupFiles)
        {
            Assert.True(File.Exists(Path.Combine(Root, file)), file);
        }
    }

    [Fact]
    public void Nothing_writes_umbras_files()
    {
        // Every file that names Umbra (the probe that reads Umbra's settings, the setup that drives Umbra, their UI)
        // writes no file at all; Umbra saves its own settings through its own code.
        var sep = Path.DirectorySeparatorChar;
        var umbraFiles = PluginFiles()
            .Concat(Directory.GetFiles(Path.Combine(Root, "Tsukimichi.Core", "Umbra"), "*.cs").Select(file => Path.GetRelativePath(Root, file)))
            .Where(file => Path.GetFileName(file).Contains("Umbra", StringComparison.Ordinal) && !file.Contains($"{sep}obj{sep}", StringComparison.Ordinal))
            .ToList();
        Assert.Contains(Path.Combine("Tsukimichi", "Game", "UmbraProbe.cs"), umbraFiles);
        Assert.Contains(Path.Combine("Tsukimichi", "Game", "UmbraControl.cs"), umbraFiles);

        var offenders = umbraFiles
            .SelectMany(file => Code(file).Split('\n').Select((line, i) => (file, i, line)))
            .Where(x => WritesFiles.IsMatch(x.line))
            .Select(x => $"{x.file}:{x.i + 1}: {x.line.Trim()}")
            .ToList();
        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void Umbra_is_driven_only_through_the_members_its_own_settings_call()
    {
        var control = Code(Path.Combine("Tsukimichi", "Game", "UmbraControl.cs"));

        // Umbra's own file and profile code is never called: its saving runs inside the members its settings use.
        foreach (var never in new[] { "\"Persist\"", "\"WriteFile\"", "\"ReadFile\"", "Profile\"", "ConfigDirectory", ".profile.json", "\"LoadWithoutRestart\"", "File." })
        {
            Assert.DoesNotContain(never, control, StringComparison.Ordinal);
        }

        foreach (var member in new[] { "\"Set\"", "\"AddEntry\"", "\"RemoveEntry\"", "\"Fetch\"", "\"DownloadRelease\"", "\"Restart\"", "\"CreateWidget\"", "\"RemoveWidget\"" })
        {
            Assert.Contains(member, control, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Umbra_changes_only_from_the_agree_button()
    {
        // The setup starts in one place: the card's Agree and add, after the pill reported the click; the service only
        // forwards it, after the quiet-moment check.
        Assert.Equal([Path.Combine("Tsukimichi", "Game", "UmbraAddonSetupService.cs"), Path.Combine("Tsukimichi", "Ui", "UmbraAddonCard.cs")], FilesWith(".AddToUmbra("));
        var footer = Member(Code(Path.Combine("Tsukimichi", "Ui", "UmbraAddonCard.cs")), "private void DrawConfirmFooter()");
        var pill = footer.IndexOf("var agree = Chrome.ActionPill(\"##umbraCardAgree\"", StringComparison.Ordinal);
        var gate = footer.IndexOf("if (agree && ready)", StringComparison.Ordinal);
        var start = footer.IndexOf("setup.AddToUmbra()", StringComparison.Ordinal);
        Assert.True(pill >= 0 && gate > pill && start > gate, "AddToUmbra must follow the Agree pill's click");
        Assert.Contains("Strings.UmbraSetupAgree", footer, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(footer, @"\.AddToUmbra\("));
        Assert.Single(Regex.Matches(Code(Path.Combine("Tsukimichi", "Ui", "UmbraAddonCard.cs")), @"\.AddToUmbra\("));

        // Undo starts in one place: Settings' Remove confirmation button.
        Assert.Equal([Path.Combine("Tsukimichi", "Game", "UmbraAddonSetupService.cs"), Path.Combine("Tsukimichi", "Ui", "ConfigWindow.UmbraAddonSetup.cs")], FilesWith(".RemoveFromUmbra("));
        var settings = Code(Path.Combine("Tsukimichi", "Ui", "ConfigWindow.UmbraAddonSetup.cs"));
        Assert.Contains("umbraSetupConfirmLabel = new(static () => Strings.UmbraSetupRemoveConfirm + ", settings, StringComparison.Ordinal);
        var confirm = settings.IndexOf("if (ImGui.Button(umbraSetupConfirmLabel.Value))", StringComparison.Ordinal);
        var remove = settings.IndexOf("setup.RemoveFromUmbra()", StringComparison.Ordinal);
        Assert.True(confirm >= 0 && remove > confirm && remove - confirm < 200, "RemoveFromUmbra must sit in the Remove confirmation's button");
        Assert.Single(Regex.Matches(settings, @"\.RemoveFromUmbra\("));

        // The service forwards each to the coordinator once, behind the quiet-moment check, and nothing else calls it.
        var service = Code(Path.Combine("Tsukimichi", "Game", "UmbraAddonSetupService.cs"));
        Assert.Contains("CanChangeNow && coordinator.AddToUmbra()", Member(service, "public bool AddToUmbra()"), StringComparison.Ordinal);
        Assert.Contains("CanChangeNow && coordinator.RemoveFromUmbra()", Member(service, "public bool RemoveFromUmbra()"), StringComparison.Ordinal);
        Assert.Single(Regex.Matches(service, @"coordinator\.AddToUmbra\("));
        Assert.Single(Regex.Matches(service, @"coordinator\.RemoveFromUmbra\("));

        // The runner is called only by the coordinator's two starts (Core), and Umbra's mutating calls only by the
        // runner: no plugin file makes them (UmbraControl.cs only declares them).
        var coordinator = Code(Path.Combine("Tsukimichi.Core", "Umbra", "UmbraSetupCoordinator.cs"));
        Assert.Contains("UmbraSetupRunner.Add(", Member(coordinator, "public Task? AddToUmbra()"), StringComparison.Ordinal);
        Assert.Contains("UmbraSetupRunner.Remove(", Member(coordinator, "public Task? RemoveFromUmbra()"), StringComparison.Ordinal);
        Assert.Single(Regex.Matches(coordinator, @"UmbraSetupRunner\.Add\("));
        Assert.Single(Regex.Matches(coordinator, @"UmbraSetupRunner\.Remove\("));
        Assert.Empty(FilesWith("UmbraSetupRunner."));
        foreach (var mutating in new[] { ".SetCustomPlugins(", ".AddRepository(", ".RemoveRepository(", ".PlaceWidget(", ".RemoveWidget(", "session.Restart(" })
        {
            Assert.Empty(FilesWith(mutating));
            Assert.DoesNotContain(mutating, coordinator, StringComparison.Ordinal);
        }

        // Before the player agrees the coordinator only opens and looks (the preview).
        var preview = Member(coordinator, "public Task RefreshPreview()");
        Assert.Equal(["Look", "Open"], Regex.Matches(preview, @"\b(?:control|session)\.(\w+)\(").Select(m => m.Groups[1].Value).Distinct().Order(StringComparer.Ordinal));

        // The reflection layer is made in one place.
        Assert.Equal([Path.Combine("Tsukimichi", "Plugin.UmbraAddonSetup.cs")], FilesWith("new Game.UmbraControl(").Concat(FilesWith("new UmbraControl(")));
    }

    [Fact]
    public void Each_run_has_its_own_umbra_and_no_wait_outlives_it()
    {
        var control = Code(Path.Combine("Tsukimichi", "Game", "UmbraControl.cs"));

        // Members live in the run's session, never in a field another run could replace mid-run.
        Assert.DoesNotContain("Members? members", control, StringComparison.Ordinal);
        Assert.Contains("new Session(this, found)", control, StringComparison.Ordinal);

        // Restart: Umbra is looked at in the same framework call that restarts it, and a restart still running refuses the next run.
        Assert.Contains("owner.OnFramework(() => Live(m) ? Started(m.Restart.Invoke(null, null)) : null)", Member(control, "public async Task<UmbraFailure> Restart()"), StringComparison.Ordinal);
        Assert.Contains("restarting is { IsCompleted: false }", Member(control, "public Task<UmbraOpened> Open()"), StringComparison.Ordinal);

        // Every timer is cancelled when its wait ends, and on unload.
        var within = Member(control, "private async Task<bool> Within(");
        Assert.Contains("CreateLinkedTokenSource(Token)", within, StringComparison.Ordinal);
        Assert.Contains("await timer.CancelAsync()", within, StringComparison.Ordinal);
        Assert.Contains("lifetime.Cancel();", Member(control, "public void Dispose()"), StringComparison.Ordinal);
        Assert.Contains("Task.Delay(SaveSettle, owner.Token)", control, StringComparison.Ordinal);
    }

    [Fact]
    public void Settings_offers_add_and_remove_only_at_quiet_moments_and_not_add_while_removing()
    {
        var settings = Code(Path.Combine("Tsukimichi", "Ui", "ConfigWindow.UmbraAddonSetup.cs"));
        Assert.Contains("else if (!removing && !umbra.AddonPresent && ShowUmbraConfirm is { } show)", settings, StringComparison.Ordinal);
        Assert.Contains("enabled: canChange, reason: Strings.UmbraSetupNotNowReason", settings, StringComparison.Ordinal);
        Assert.Contains("enabled: canChange || removing, reason: Strings.UmbraSetupNotNowReason", settings, StringComparison.Ordinal);
        Assert.Contains("var canAsk = here && state.Activity == UmbraSetupActivity.Idle && canChange;", settings, StringComparison.Ordinal);

        // The card never stays on its progress once nothing runs.
        var draw = Member(Code(Path.Combine("Tsukimichi", "Ui", "UmbraAddonCard.cs")), "public override void Draw()");
        Assert.Contains("if (page == Page.Progress && state.Activity == UmbraSetupActivity.Idle)", draw, StringComparison.Ordinal);
    }

    [Fact]
    public void The_card_answers_to_no_key_and_stays_answered()
    {
        var card = Code(Path.Combine("Tsukimichi", "Ui", "UmbraAddonCard.cs"));
        foreach (var key in new[] { "ImGuiKey", "KeyState", "IsKeyPressed", "VirtualKey" })
        {
            Assert.DoesNotContain(key, card, StringComparison.Ordinal);
        }

        Assert.Contains("RespectCloseHotkey = false;", card, StringComparison.Ordinal);
        Assert.Contains("Record();", Member(card, "public override void OnClose()"), StringComparison.Ordinal);
        Assert.Contains("settings.UmbraSetupOfferAnswered = true;", Member(card, "private void Record()"), StringComparison.Ordinal);
        Assert.Contains("settings.Save(pluginInterface);", Member(card, "private void Record()"), StringComparison.Ordinal);
        Assert.Contains("settings.UmbraSetupOfferAnswered", Member(card, "public override void PreOpenCheck()"), StringComparison.Ordinal);
    }

    [Fact]
    public void The_card_waits_for_whats_new_and_the_tour()
    {
        var wiring = Code(Path.Combine("Tsukimichi", "Plugin.UmbraAddonSetup.cs"));
        Assert.Contains("OtherFirst = () => whatsNewPopup?.Due == true || tutorial?.Active == true,", wiring, StringComparison.Ordinal);
        Assert.Contains("Moment = WhatsNewMomentNow,", wiring, StringComparison.Ordinal);
    }

    [Fact]
    public void The_setup_adds_no_network_code()
    {
        var files = SetupFiles.Select(file => (file, Read(file)));
        var offenders = NoNetworkTests.Offenders(files);
        Assert.True(offenders.Count == 0, string.Join(Environment.NewLine, offenders));

        foreach (var file in SetupFiles)
        {
            var source = Read(file);
            Assert.DoesNotContain("api.github.com", source, StringComparison.Ordinal);

            // The one address is the repository page, opened in the player's browser by Dalamud, never fetched.
            foreach (Match match in Address.Matches(source))
            {
                Assert.Equal("https://github.com/", match.Value);
            }
        }

        var card = Code(Path.Combine("Tsukimichi", "Ui", "UmbraAddonCard.cs"));
        Assert.Contains("Util.OpenLink(url);", Member(card, "private static void DrawLink(string url)"), StringComparison.Ordinal);
        Assert.Single(Regex.Matches(card, @"Util\.OpenLink\("));
    }
}
