using System.Globalization;
using Lumina.Data;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Unlocks;

namespace Tsukimichi.GameData;

/// <summary>
/// The game's own art for unique rewards (Moonlit, feature plan v6 G6), read from the sheets once, off the frame, for
/// the rewards the shipped and curated data name. Each reward kind reads its own sheet, so a reward shows its own art
/// rather than the generic icon of the item that unlocks it:
/// <list type="bullet">
/// <item>mounts and minions their guide icon (<c>Mount.Icon</c>, <c>Companion.Icon</c>) and, for the gallery, the
/// 384 px guide picture beside it (icon + <see cref="GuideArtOffset"/>);</item>
/// <item>emotes <c>Emote.Icon</c>; orchestrion rolls their category's icon (<c>OrchestrionUiparam</c> →
/// <c>OrchestrionCategory.Icon</c>); Triple Triad cards their card icon (<see cref="CardIconBase"/> + card id);
/// bardings <c>BuddyEquip</c> (body, else head, else legs); fashion accessories <c>Ornament.Icon</c>; hairstyles the
/// first <c>CharaMakeCustomize</c> row unlocked by the reward;</item>
/// <item>duties their content type's icon, jobs the 062100-series job icon, aether currents the attunement crystal,
/// traits, achievements, actions, general actions, blue mage spells and other rewards their sheet icon, items and
/// titles (the achievement named in the entry's source) theirs;</item>
/// <item>the MainCommand menu icons, which the kinds list wears (<see cref="MoonlitKindIcons.MainCommandRow"/>);</item>
/// <item>and the icons system unlocks wear (<see cref="FeatureArt"/>, read by <see cref="FeatureIconReader"/>).</item>
/// </list>
/// Each part is read on its own: a sheet that cannot be read leaves only its kind out, and is logged. Immutable once
/// built, so safe to read from any thread. Standalone (takes an <see cref="ExcelModule"/>) so tests build it against
/// game data without Dalamud.
/// </summary>
public sealed class RewardArtIndex
{
    /// <summary>A mount's or minion's guide icon (004xxx) plus this is its 384 px guide picture (068xxx).</summary>
    public const uint GuideArtOffset = 64000;

    /// <summary>A Triple Triad card's square 80 px icon is this plus the card's row id (088001 is the Dodo).</summary>
    public const uint CardIconBase = 88000;

    /// <summary>The aether current attunement crystal in the 060000 icon set.</summary>
    public const uint AetherCurrentIcon = 60033;

    /// <summary>First job icon in the 062000 set: 062101 Gladiator … 062142 Pictomancer, offset by ClassJob row id.</summary>
    public const uint ClassJobIconBase = 62100;

    /// <summary><c>ContentFinderCondition.ContentLinkType</c> value whose <c>Content</c> is an InstanceContent row.</summary>
    private const byte InstanceContentLink = 1;

    private readonly Dictionary<(RewardKind Kind, uint Id), uint> icons;
    private readonly Dictionary<(RewardKind Kind, uint Id), uint> art;
    private readonly Dictionary<uint, uint> menus;
    private readonly FeatureIcons features;

    private RewardArtIndex(Dictionary<(RewardKind, uint), uint> icons, Dictionary<(RewardKind, uint), uint> art, Dictionary<uint, uint> menus, FeatureIcons features)
    {
        this.icons = icons;
        this.art = art;
        this.menus = menus;
        this.features = features;
    }

    /// <summary>No art at all (no game data, or nothing could be read).</summary>
    public static RewardArtIndex Empty { get; } = new([], [], [], FeatureIcons.Empty);

    /// <summary>How many rewards have an icon here.</summary>
    public int Count => icons.Count;

    /// <summary>How many rewards have a large picture here.</summary>
    public int ArtCount => art.Count;

    /// <summary>
    /// The reward's own square icon; 0 when the index has none. Items (and titles, through their achievement) are
    /// keyed by their item (achievement) id: see <see cref="KeyOf"/>.
    /// </summary>
    public uint Icon(RewardKind kind, uint id) => icons.GetValueOrDefault((kind, id));

    /// <summary>The reward's large square picture (a mount's or minion's guide art); 0 when it has none.</summary>
    public uint Art(RewardKind kind, uint id) => art.GetValueOrDefault((kind, id));

    /// <summary>The icon of a MainCommand (menu) row; 0 when unknown.</summary>
    public uint MenuIcon(uint mainCommandRow) => menus.GetValueOrDefault(mainCommandRow);

    /// <summary>The icon a system unlock's label wears (<see cref="FeatureArt"/>); 0 when none fits.</summary>
    public uint FeatureIcon(string? label) => features.For(label);

    /// <summary>
    /// The (kind, id) an entry's own art is filed under: the item for an item reward, the achievement named in the
    /// source for a title, the reward id otherwise; (kind, 0) when there is nothing to look up.
    /// </summary>
    public static (RewardKind Kind, uint Id) KeyOf(UniqueRewardEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return entry.Kind switch
        {
            RewardKind.Item or RewardKind.OptionalItem or RewardKind.ArtifactGear => (RewardKind.Item, entry.ItemId),
            RewardKind.Title => (RewardKind.Achievement, AchievementOf(entry.Source)),
            _ => (entry.Kind, entry.RewardId),
        };
    }

    /// <summary>The achievement a title entry's source names (<c>Achievement.Key;achievement=1030;type=6</c>); 0 when none.</summary>
    public static uint AchievementOf(string? source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return 0;
        }

        foreach (var part in source.Split(';'))
        {
            const string Prefix = "achievement=";
            if (part.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
                && uint.TryParse(part.AsSpan(Prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            {
                return id;
            }
        }

        return 0;
    }

    /// <summary>
    /// Reads the art of <paramref name="entries"/> (the rewards Moonlit lists) and every menu icon.
    /// </summary>
    /// <param name="excel">The game's sheets.</param>
    /// <param name="language">The client's language: most of these sheets exist only per language.</param>
    /// <param name="entries">The rewards to read art for.</param>
    /// <param name="iconExists">Whether the game has an icon (its texture file); a guide picture is kept only when it
    /// does, as is a card icon. Null keeps no guide pictures and trusts card icons.</param>
    /// <param name="log">Told about a part that could not be read.</param>
    public static RewardArtIndex Build(ExcelModule excel, Language language, IEnumerable<UniqueRewardEntry> entries, Func<uint, bool>? iconExists = null, System.Action<string>? log = null)
    {
        ArgumentNullException.ThrowIfNull(excel);
        ArgumentNullException.ThrowIfNull(entries);

        var wanted = new Dictionary<RewardKind, HashSet<uint>>();
        foreach (var entry in entries)
        {
            var (kind, id) = KeyOf(entry);
            if (id == 0)
            {
                continue;
            }

            if (!wanted.TryGetValue(kind, out var ids))
            {
                ids = [];
                wanted[kind] = ids;
            }

            ids.Add(id);
        }

        var icons = new Dictionary<(RewardKind, uint), uint>();
        var art = new Dictionary<(RewardKind, uint), uint>();
        var menus = new Dictionary<uint, uint>();

        void Part(string what, System.Action read)
        {
            try
            {
                read();
            }
            catch (Exception ex)
            {
                log?.Invoke($"Reward art: {what} could not be read; those rewards show their kind's icon ({ex.GetBaseException().Message})");
            }
        }

        IEnumerable<uint> Ids(RewardKind kind) => wanted.TryGetValue(kind, out var ids) ? ids : [];

        void Put(RewardKind kind, uint id, uint icon)
        {
            if (icon != 0)
            {
                icons[(kind, id)] = icon;
            }
        }

        void Guide(RewardKind kind, uint id, uint icon)
        {
            Put(kind, id, icon);
            if (icon != 0 && iconExists is not null && iconExists(icon + GuideArtOffset))
            {
                art[(kind, id)] = icon + GuideArtOffset;
            }
        }

        Part("mounts", () =>
        {
            var sheet = excel.GetSheet<Mount>(language);
            foreach (var id in Ids(RewardKind.Mount))
            {
                Guide(RewardKind.Mount, id, (uint)(sheet.GetRowOrDefault(id)?.Icon ?? 0));
            }
        });
        Part("minions", () =>
        {
            var sheet = excel.GetSheet<Companion>(language);
            foreach (var id in Ids(RewardKind.Minion))
            {
                Guide(RewardKind.Minion, id, (uint)(sheet.GetRowOrDefault(id)?.Icon ?? 0));
            }
        });
        Part("emotes", () =>
        {
            var sheet = excel.GetSheet<Emote>(language);
            foreach (var id in Ids(RewardKind.Emote))
            {
                Put(RewardKind.Emote, id, (uint)(sheet.GetRowOrDefault(id)?.Icon ?? 0));
            }
        });
        Part("orchestrion rolls", () =>
        {
            var sheet = excel.GetSheet<OrchestrionUiparam>(language);
            foreach (var id in Ids(RewardKind.Orchestrion))
            {
                Put(RewardKind.Orchestrion, id, sheet.GetRowOrDefault(id)?.OrchestrionCategory.ValueNullable?.Icon ?? 0u);
            }
        });
        Part("Triple Triad cards", () =>
        {
            var sheet = excel.GetSheet<TripleTriadCard>(language);
            foreach (var id in Ids(RewardKind.TripleTriadCard))
            {
                var icon = CardIconBase + id;
                if (sheet.HasRow(id) && (iconExists is null || iconExists(icon)))
                {
                    Put(RewardKind.TripleTriadCard, id, icon);
                }
            }
        });
        Part("bardings", () =>
        {
            var sheet = excel.GetSheet<BuddyEquip>(language);
            foreach (var id in Ids(RewardKind.Barding))
            {
                if (sheet.GetRowOrDefault(id) is { } barding)
                {
                    var icon = barding.IconBody > 0 ? barding.IconBody : barding.IconHead > 0 ? barding.IconHead : barding.IconLegs;
                    Put(RewardKind.Barding, id, (uint)Math.Max(0, (int)icon));
                }
            }
        });
        Part("fashion accessories", () =>
        {
            var sheet = excel.GetSheet<Ornament>(language);
            foreach (var id in Ids(RewardKind.Ornament))
            {
                Put(RewardKind.Ornament, id, (uint)(sheet.GetRowOrDefault(id)?.Icon ?? 0));
            }
        });
        Part("hairstyles", () =>
        {
            var hairstyles = Ids(RewardKind.Hairstyle).ToHashSet();
            if (hairstyles.Count == 0)
            {
                return;
            }

            // The same hairstyle has a row per race and gender; the first row with an icon stands for it.
            foreach (var row in excel.GetSheet<CharaMakeCustomize>(language))
            {
                var link = (uint)row.UnlockLink;
                if (row.Icon != 0 && hairstyles.Contains(link) && !icons.ContainsKey((RewardKind.Hairstyle, link)))
                {
                    Put(RewardKind.Hairstyle, link, row.Icon);
                }
            }
        });
        Part("duties", () =>
        {
            var duties = Ids(RewardKind.DutyUnlock).ToHashSet();
            var instances = Ids(RewardKind.Instance).ToHashSet();
            if (duties.Count == 0 && instances.Count == 0)
            {
                return;
            }

            foreach (var row in excel.GetSheet<ContentFinderCondition>(language))
            {
                var icon = row.ContentType.ValueNullable?.Icon ?? 0u;
                if (icon == 0)
                {
                    continue;
                }

                if (duties.Contains(row.RowId))
                {
                    Put(RewardKind.DutyUnlock, row.RowId, icon);
                }

                if (row.ContentLinkType == InstanceContentLink && instances.Contains(row.Content.RowId) && !icons.ContainsKey((RewardKind.Instance, row.Content.RowId)))
                {
                    Put(RewardKind.Instance, row.Content.RowId, icon);
                }
            }
        });
        Part("jobs", () =>
        {
            var sheet = excel.GetSheet<ClassJob>(language);
            foreach (var id in Ids(RewardKind.ClassJob))
            {
                if (sheet.HasRow(id))
                {
                    Put(RewardKind.ClassJob, id, ClassJobIconBase + id);
                }
            }
        });
        foreach (var id in Ids(RewardKind.AetherCurrent))
        {
            Put(RewardKind.AetherCurrent, id, AetherCurrentIcon);
        }

        Part("traits", () =>
        {
            var sheet = excel.GetSheet<Trait>(language);
            foreach (var id in Ids(RewardKind.Trait))
            {
                Put(RewardKind.Trait, id, (uint)Math.Max(0, sheet.GetRowOrDefault(id)?.Icon ?? 0));
            }
        });
        Part("achievements", () =>
        {
            var sheet = excel.GetSheet<Achievement>(language);
            foreach (var id in Ids(RewardKind.Achievement))
            {
                Put(RewardKind.Achievement, id, (uint)(sheet.GetRowOrDefault(id)?.Icon ?? 0));
            }
        });
        Part("actions", () =>
        {
            var sheet = excel.GetSheet<Lumina.Excel.Sheets.Action>(language);
            foreach (var id in Ids(RewardKind.Action))
            {
                Put(RewardKind.Action, id, (uint)(sheet.GetRowOrDefault(id)?.Icon ?? 0));
            }
        });
        Part("general actions", () =>
        {
            var sheet = excel.GetSheet<GeneralAction>(language);
            foreach (var id in Ids(RewardKind.GeneralAction))
            {
                Put(RewardKind.GeneralAction, id, (uint)Math.Max(0, sheet.GetRowOrDefault(id)?.Icon ?? 0));
            }
        });
        Part("blue mage spells", () =>
        {
            var sheet = excel.GetSheet<AozAction>(language);
            foreach (var id in Ids(RewardKind.BlueMageSpell))
            {
                Put(RewardKind.BlueMageSpell, id, (uint)(sheet.GetRowOrDefault(id)?.Action.ValueNullable?.Icon ?? 0));
            }
        });
        Part("other rewards", () =>
        {
            var sheet = excel.GetSheet<QuestRewardOther>(language);
            foreach (var id in Ids(RewardKind.Other))
            {
                Put(RewardKind.Other, id, sheet.GetRowOrDefault(id)?.Icon ?? 0u);
            }
        });
        Part("items", () =>
        {
            var sheet = excel.GetSheet<Item>(language);
            foreach (var id in Ids(RewardKind.Item))
            {
                Put(RewardKind.Item, id, (uint)(sheet.GetRowOrDefault(id)?.Icon ?? 0));
            }
        });
        Part("menus", () =>
        {
            foreach (var row in excel.GetSheet<MainCommand>(language))
            {
                if (row.Icon > 0)
                {
                    menus[row.RowId] = (uint)row.Icon;
                }
            }
        });

        var features = FeatureIconReader.Read(excel, language, log);
        return new RewardArtIndex(icons, art, menus, features);
    }

    /// <summary>The game path of an icon's texture (the normal resolution), for an <c>iconExists</c> check.</summary>
    public static string IconPath(uint iconId) =>
        string.Create(CultureInfo.InvariantCulture, $"ui/icon/{iconId / 1000 * 1000:D6}/{iconId:D6}.tex");
}
