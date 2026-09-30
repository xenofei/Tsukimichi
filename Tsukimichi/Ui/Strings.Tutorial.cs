namespace Tsukimichi.Ui;

/// <summary>
/// The interactive tour's copy (T14, accessibility C1, game UX panel finding 6): three chapters, Find, Read and
/// Beyond, each step titled by what the player gets rather than by the widget, every body at most 35 words (a test
/// counts them). Value first, the moon metaphor saved for the finish card.
/// </summary>
static partial class Strings
{
    public static class Tutorial
    {
        public const string CardId = "##TsukimichiTutorialCard";

        // Offer (first run)
        public const string TakeTour = "Take the tour";
        public const string Later = "Later";
        public const string DontOffer = "Don't offer again";
        public const string LaterTooltip = "Offer the tour again next session";
        public const string DontOfferTooltip = "Never offer it again; the toolbar's cap and Settings still start it";

        // Buttons and progress
        public const string Back = "Back";
        public const string Next = "Next";
        public const string Close = "Close";
        public const string Done = "Done";
        public const string OpenHelp = "Open help";
        public const string ChapterFind = "Find";
        public const string ChapterRead = "Read";
        public const string ChapterBeyond = "Beyond";
        public const string ChapterTooltip = "Jump to this chapter";
        /// <summary>{0} = chapter, {1} = step within the chapter, {2} = steps in the chapter.</summary>
        public const string ProgressFormat = "{0} · step {1} of {2}";
        public const string KeysHint = "Enter or → next · ← back · Esc closes";
        public const string OfferKeysHint = "Enter takes the tour · Esc: later";

        // ---- Find ----
        public const string WelcomeTitle = "Welcome to Tsukimichi";
        public const string WelcomeBody = "Tsukimichi shows what you can pick up now, why a quest is locked, and which rewards exist nowhere else. Three short chapters; nothing in the tour changes your game.";
        public const string SearchTitle = "Find a quest by name";
        public const string SearchBody = "Type a quest, a reward or an id and the table narrows as you type. Ctrl+F jumps here; the × clears it.";
        public const string QuickViewsTitle = "One click to what matters";
        public const string QuickViewsBody = "Quick views answer the common questions: Unlocks, quests at My level, Stalled ones, Story sidequests and Sprout mode. All turns them off.";
        public const string FiltersTitle = "Narrow it further";
        public const string FiltersBody = "Filters opens this panel beside the tree. The badge counts what narrows the table, and each chip under the toolbar clears one; the first chip is the tree scope.";
        public const string TabsTitle = "Where things live";
        public const string TabsBody = "Journal holds every quest; its badge counts the quests you can pick up now. Moonlit, Characters, Flight and My blues come in the last chapter.";
        public const string TreeTitle = "Pick a part of the journal";
        public const string TreeBody = "Sections, categories and genres narrow the table. Each ring fills as you complete its quests; a gold count means quests you can accept now.";
        public const string TableTitle = "Every quest, one row each";
        public const string TableBody = "Click a header to sort and a row to read it. Right-click a row, or use the … at its end or the Menu key, for pin, map flag and the in-game journal.";

        // ---- Read ----
        public const string DetailTitle = "Why not this one";
        public const string DetailBody = "Select a row: the detail pane stacks cards, requirements first, with the one that blocks you marked. With nothing selected, Tonight shows what you can do now.";
        public const string StatusTitle = "The Status column";
        public const string StatusBody = "Status says in one clause what stands between you and a quest: a level, a rank, an earlier quest, or the step you are on.";
        public const string LegendTitle = "Eight moons, eight states";
        public const string LegendBody = "Every state has its own shape, so colour is never needed to tell them apart. Hover a moon anywhere for its name.";
        public const string PathTitle = "What leads here";
        public const string PathBody = "The path is a star chart: earlier quests as moons on a gold thread where walked, finished runs folded into beads, then what this one opens. The bar below flags the giver.";

        // ---- Beyond ----
        public const string MoonlitTitle = "Rewards found nowhere else";
        public const string MoonlitBody = "Moonlit lists quest rewards that exist nowhere else, grouped by kind, with the ones you already own marked.";
        public const string CharactersTitle = "Every character, even logged out";
        public const string CharactersBody = "Each character you log in keeps a snapshot here. Pick one to browse the journal as that character, with a dashboard of its progress.";
        public const string FlightTitle = "Unlock flying";
        public const string FlightBody = "Pick a zone to see its aether current quests, what blocks each one, and where to fly next.";
        public const string PlanTitle = "Unlock quests left to clear";
        public const string PlanBody = "My blues lists every blue unlock quest you have left, by expansion and zone, with what each one opens. Copy it as a checklist or pin an expansion to the Todo overlay.";
        public const string PlayTitle = "While you play";
        public const string PlayBody = "The Todo overlay keeps pins and nearby unlocks on screen; Nearby lists what you can start in this zone. Turn them on in Settings, or type /tsuki todo and /tsuki nearby.";
        public const string HelpTitle = "Help, tour and settings";
        public const string HelpBody = "The question mark opens the guide, the cap replays this tour, and the cog opens Settings for text size, colours and spoilers.";
        public const string FinishTitle = "That is the road";
        public const string FinishBody = "Every quest is a step, and the moon fills as you walk. Reopen this tour from the toolbar or Settings; Help has more on every topic.";
    }
}
