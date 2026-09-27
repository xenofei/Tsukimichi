using Lumina;
using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace Tsukimichi.DataGen;

/// <summary>Opens the local game install and exposes every sheet the generator reads.</summary>
internal sealed class GameSheets
{
    public GameData Data { get; }
    public string GameVersion { get; }

    public ExcelSheet<Quest> Quests { get; }
    public ExcelSheet<Item> Items { get; }
    public ExcelSheet<ItemAction> ItemActions { get; }
    public ExcelSheet<Emote> Emotes { get; }
    public ExcelSheet<Lumina.Excel.Sheets.Action> Actions { get; }
    public ExcelSheet<GeneralAction> GeneralActions { get; }
    public ExcelSheet<Trait> Traits { get; }
    public ExcelSheet<ClassJob> ClassJobs { get; }
    public ExcelSheet<AetherCurrent> AetherCurrents { get; }
    public ExcelSheet<AetherCurrentCompFlgSet> AetherCurrentSets { get; }
    public ExcelSheet<AozActionTransient> AozActionTransients { get; }
    public ExcelSheet<AozAction> AozActions { get; }
    public ExcelSheet<Achievement> Achievements { get; }
    public ExcelSheet<Title> Titles { get; }
    public ExcelSheet<ContentFinderCondition> ContentFinderConditions { get; }
    public ExcelSheet<InstanceContent> InstanceContents { get; }
    public SubrowExcelSheet<GilShopItem> GilShopItems { get; }
    public ExcelSheet<SpecialShop> SpecialShops { get; }
    public ExcelSheet<Recipe> Recipes { get; }
    public ExcelSheet<GatheringItem> GatheringItems { get; }
    public SubrowExcelSheet<QuestClassJobReward> QuestClassJobRewards { get; }
    public ExcelSheet<QuestRewardOther> QuestRewardOthers { get; }
    public ExcelSheet<JournalGenre> JournalGenres { get; }
    public ExcelSheet<JournalCategory> JournalCategories { get; }
    public ExcelSheet<JournalSection> JournalSections { get; }
    public ExcelSheet<Mount> Mounts { get; }
    public ExcelSheet<Companion> Companions { get; }
    public ExcelSheet<Orchestrion> Orchestrions { get; }
    public ExcelSheet<TripleTriadCard> TripleTriadCards { get; }
    public ExcelSheet<Ornament> Ornaments { get; }
    public ExcelSheet<BuddyEquip> BuddyEquips { get; }
    public ExcelSheet<CharaMakeCustomize> CharaMakeCustomizes { get; }

    public GameSheets(string sqpackPath)
    {
        Data = new GameData(sqpackPath, new LuminaOptions { PanicOnSheetChecksumMismatch = false });
        GameVersion = ReadGameVersion(sqpackPath);

        Quests = Sheet<Quest>();
        Items = Sheet<Item>();
        ItemActions = Sheet<ItemAction>();
        Emotes = Sheet<Emote>();
        Actions = Sheet<Lumina.Excel.Sheets.Action>();
        GeneralActions = Sheet<GeneralAction>();
        Traits = Sheet<Trait>();
        ClassJobs = Sheet<ClassJob>();
        AetherCurrents = Sheet<AetherCurrent>();
        AetherCurrentSets = Sheet<AetherCurrentCompFlgSet>();
        AozActionTransients = Sheet<AozActionTransient>();
        AozActions = Sheet<AozAction>();
        Achievements = Sheet<Achievement>();
        Titles = Sheet<Title>();
        ContentFinderConditions = Sheet<ContentFinderCondition>();
        InstanceContents = Sheet<InstanceContent>();
        GilShopItems = SubrowSheet<GilShopItem>();
        SpecialShops = Sheet<SpecialShop>();
        Recipes = Sheet<Recipe>();
        GatheringItems = Sheet<GatheringItem>();
        QuestClassJobRewards = SubrowSheet<QuestClassJobReward>();
        QuestRewardOthers = Sheet<QuestRewardOther>();
        JournalGenres = Sheet<JournalGenre>();
        JournalCategories = Sheet<JournalCategory>();
        JournalSections = Sheet<JournalSection>();
        Mounts = Sheet<Mount>();
        Companions = Sheet<Companion>();
        Orchestrions = Sheet<Orchestrion>();
        TripleTriadCards = Sheet<TripleTriadCard>();
        Ornaments = Sheet<Ornament>();
        BuddyEquips = Sheet<BuddyEquip>();
        CharaMakeCustomizes = Sheet<CharaMakeCustomize>();
    }

    private ExcelSheet<T> Sheet<T>() where T : struct, IExcelRow<T>
        => Data.GetExcelSheet<T>(Language.English) ?? throw new InvalidOperationException($"Sheet {typeof(T).Name} is missing from the game data.");

    private SubrowExcelSheet<T> SubrowSheet<T>() where T : struct, IExcelSubrow<T>
        => Data.GetSubrowExcelSheet<T>(Language.English) ?? throw new InvalidOperationException($"Subrow sheet {typeof(T).Name} is missing from the game data.");

    /// <summary>The game directory holds ffxivgame.ver one level above sqpack; the file is a single line like 2026.09.15.0000.0000.</summary>
    private static string ReadGameVersion(string sqpackPath)
    {
        var full = Path.GetFullPath(sqpackPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var gameDir = Path.GetDirectoryName(full) ?? full;
        var verFile = Path.Combine(gameDir, "ffxivgame.ver");
        if (!File.Exists(verFile))
            return "unknown";
        var line = File.ReadLines(verFile).FirstOrDefault()?.Trim();
        return string.IsNullOrEmpty(line) ? "unknown" : line;
    }
}
