using Tsukimichi.Core.Umbra;

namespace Tsukimichi.Tests.Umbra;

/// <summary>
/// The reads of Umbra's settings (plan v8 M1 and M3; <see cref="UmbraReads"/>, driven by the plugin's UmbraProbe): a read
/// asked for while one runs is kept (a character change mid-read), a failed re-read of the same file keeps the last good
/// read once and retries, nothing keeps clear before the first read lands, and the add-on counts as installed only with
/// Umbra's custom plugins on.
/// </summary>
public sealed class UmbraReadsTests
{
    private const string Default = "Default.profile.json";
    private const string Alt = "Alt.profile.json";

    private static readonly UmbraRead Good = new(
        new UmbraToolbar(Enabled: true, TopAligned: false, AutoHide: false, Stretched: true, Height: 32, YOffset: 0, UiScalePercent: 100),
        Colors: null,
        CustomPluginsOn: true,
        AddonListed: true,
        Problem: "no colour profile");

    private static readonly UmbraRead Failed = UmbraRead.Failed("not JSON");

    [Fact]
    public void A_read_asked_for_while_one_runs_starts_when_that_one_lands()
    {
        var reads = new UmbraReads();
        Assert.True(reads.Request());
        Assert.True(reads.Reading);

        // The character changes mid-read: not dropped, but held.
        Assert.False(reads.Request());
        var landing = reads.Land(null, null, Good, Default);
        Assert.True(landing.Take);
        Assert.True(landing.StartAgain);
        Assert.True(reads.Reading);

        // That second read lands with nothing more asked for.
        landing = reads.Land(Good, Default, Good, Alt);
        Assert.True(landing.Take);
        Assert.False(landing.StartAgain);
        Assert.False(reads.Reading);
    }

    [Fact]
    public void A_failed_re_read_of_the_same_file_keeps_the_last_good_read_once_and_retries()
    {
        var reads = new UmbraReads();
        reads.Request();
        var landing = reads.Land(Good, Default, Failed, Default);
        Assert.False(landing.Take);
        Assert.True(landing.RetrySoon);

        // The retry fails too: the file really is unreadable, and that is taken.
        reads.Request();
        landing = reads.Land(Good, Default, Failed, Default);
        Assert.True(landing.Take);
        Assert.False(landing.RetrySoon);

        // Another file (another character's profile) that fails is taken at once; so is a first read that fails.
        reads.Request();
        Assert.True(reads.Land(Good, Default, Failed, Alt).Take);
        reads.Request();
        Assert.True(reads.Land(null, null, Failed, Default).Take);
    }

    [Fact]
    public void Nothing_keeps_clear_before_the_first_read_lands()
    {
        var reads = new UmbraReads();
        Assert.False(reads.Settled);
        Assert.False(UmbraClearance.For(umbraLoaded: reads.Settled, toolbar: null, assumedHeight: 32, uiScale: 1f).Any);

        reads.Request();
        reads.Land(null, null, Good, Default);
        Assert.True(reads.Settled);

        // Umbra unloads and comes back: it waits for its own first read again.
        reads.Unloaded();
        Assert.False(reads.Settled);
    }

    [Fact]
    public void The_add_on_counts_only_with_Umbras_custom_plugins_on()
    {
        Assert.True(UmbraReads.AddonPresent(null, Good));
        Assert.False(UmbraReads.AddonPresent(null, Good with { CustomPluginsOn = false }));
        Assert.False(UmbraReads.AddonPresent(null, Good with { AddonListed = false }));
        Assert.False(UmbraReads.AddonPresent(null, null));

        // Its hello says it runs.
        Assert.True(UmbraReads.AddonPresent("1.0.0", null));
    }

    [Fact]
    public void A_hello_goes_when_Umbra_unloads_or_its_settings_show_the_add_on_off()
    {
        Assert.True(UmbraReads.HelloStands(umbraLoaded: true, Good));
        Assert.False(UmbraReads.HelloStands(umbraLoaded: false, Good));
        Assert.False(UmbraReads.HelloStands(true, Good with { CustomPluginsOn = false }));
        Assert.False(UmbraReads.HelloStands(true, Good with { AddonListed = false }));

        // A read that found nothing says nothing about the add-on.
        Assert.True(UmbraReads.HelloStands(true, Failed));
        Assert.True(UmbraReads.HelloStands(true, null));
    }
}
