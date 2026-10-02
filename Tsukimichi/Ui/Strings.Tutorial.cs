using Tsukimichi.Localization;

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
        public static string TakeTour => Loc.Get("Tutorial.TakeTour");
        public static string Later => Loc.Get("Tutorial.Later");
        public static string DontOffer => Loc.Get("Tutorial.DontOffer");
        public static string LaterTooltip => Loc.Get("Tutorial.LaterTooltip");
        public static string DontOfferTooltip => Loc.Get("Tutorial.DontOfferTooltip");

        // Buttons and progress
        public static string Back => Loc.Get("Tutorial.Back");
        public static string Next => Loc.Get("Tutorial.Next");
        public static string Close => Loc.Get("Tutorial.Close");
        public static string Done => Loc.Get("Tutorial.Done");
        public static string OpenHelp => Loc.Get("Tutorial.OpenHelp");
        public static string ChapterFind => Loc.Get("Tutorial.ChapterFind");
        public static string ChapterRead => Loc.Get("Tutorial.ChapterRead");
        public static string ChapterBeyond => Loc.Get("Tutorial.ChapterBeyond");
        public static string ChapterTooltip => Loc.Get("Tutorial.ChapterTooltip");
        /// <summary>{0} = chapter, {1} = step within the chapter, {2} = steps in the chapter.</summary>
        public static string ProgressFormat => Loc.Get("Tutorial.ProgressFormat");
        public static string KeysHint => Loc.Get("Tutorial.KeysHint");
        public static string OfferKeysHint => Loc.Get("Tutorial.OfferKeysHint");

        // ---- Find ----
        public static string WelcomeTitle => Loc.Get("Tutorial.WelcomeTitle");
        public static string WelcomeBody => Loc.Get("Tutorial.WelcomeBody");
        public static string SearchTitle => Loc.Get("Tutorial.SearchTitle");
        public static string SearchBody => Loc.Get("Tutorial.SearchBody");
        public static string QuickViewsTitle => Loc.Get("Tutorial.QuickViewsTitle");
        public static string QuickViewsBody => Loc.Get("Tutorial.QuickViewsBody");
        public static string FiltersTitle => Loc.Get("Tutorial.FiltersTitle");
        public static string FiltersBody => Loc.Get("Tutorial.FiltersBody");
        public static string TabsTitle => Loc.Get("Tutorial.TabsTitle");
        public static string TabsBody => Loc.Get("Tutorial.TabsBody");
        public static string TreeTitle => Loc.Get("Tutorial.TreeTitle");
        public static string TreeBody => Loc.Get("Tutorial.TreeBody");
        public static string TableTitle => Loc.Get("Tutorial.TableTitle");
        public static string TableBody => Loc.Get("Tutorial.TableBody");

        // ---- Read ----
        public static string DetailTitle => Loc.Get("Tutorial.DetailTitle");
        public static string DetailBody => Loc.Get("Tutorial.DetailBody");
        public static string StatusTitle => Loc.Get("Tutorial.StatusTitle");
        public static string StatusBody => Loc.Get("Tutorial.StatusBody");
        public static string LegendTitle => Loc.Get("Tutorial.LegendTitle");
        public static string LegendBody => Loc.Get("Tutorial.LegendBody");
        public static string PathTitle => Loc.Get("Tutorial.PathTitle");
        public static string PathBody => Loc.Get("Tutorial.PathBody");

        // ---- Beyond ----
        public static string MoonlitTitle => Loc.Get("Tutorial.MoonlitTitle");
        public static string MoonlitBody => Loc.Get("Tutorial.MoonlitBody");
        public static string CharactersTitle => Loc.Get("Tutorial.CharactersTitle");
        public static string CharactersBody => Loc.Get("Tutorial.CharactersBody");
        public static string FlightTitle => Loc.Get("Tutorial.FlightTitle");
        public static string FlightBody => Loc.Get("Tutorial.FlightBody");
        public static string PlanTitle => Loc.Get("Tutorial.PlanTitle");
        public static string PlanBody => Loc.Get("Tutorial.PlanBody");
        public static string PlayTitle => Loc.Get("Tutorial.PlayTitle");
        public static string PlayBody => Loc.Get("Tutorial.PlayBody");
        public static string CompanionsTitle => Loc.Get("Tutorial.CompanionsTitle");
        public static string CompanionsBody => Loc.Get("Tutorial.CompanionsBody");
        public static string HelpTitle => Loc.Get("Tutorial.HelpTitle");
        public static string HelpBody => Loc.Get("Tutorial.HelpBody");
        public static string FinishTitle => Loc.Get("Tutorial.FinishTitle");
        public static string FinishBody => Loc.Get("Tutorial.FinishBody");
    }
}
