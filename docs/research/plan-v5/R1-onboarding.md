# R1 Onboarding — summary
F1 Tour "Read" chapter targets DetailRequirements/Path/Giver only recorded when a quest selected; tour never selects -> points at Tonight card (TutorialOverlay.cs:111,137-140; DetailPane.cs:247,277,283; MainWindow.cs:1318).
F2 Todo overlay off by default (Configuration.cs:81), no main-window toggle; Pin tooltip promises overlay; Nearby only via /tsuki nearby or DTR (Plugin.cs:653); tour "While you play" has no button.
F3 Help lacks topics for item hints, NPC "quests here", Duty Finder hint filed under Known quirks; Route & Abandoned buried; HelpActions can't toggle overlay/open Nearby (HelpWindow.cs:45).
F4 Settings one 13-section scroll, Polling first, Nearby settings elsewhere (ConfigWindow.cs:184-211; DiscoveryWindow.cs:272).
F5 What's new shows only running version (WhatsNewCard.cs:84) — 1.4.1 updaters miss 1.2–1.4; bullets too long.
F6 Unknown subcommands become searches (TsukimichiCommand.cs:250); no tab/tour subcommands; /tsuki report missing from README; /tsuki glyphs listed to players.
F7 ShowHelpOnFirstRun never read (Configuration.cs:202); browse-mode banner jargon; Ctrl+1..4 comments vs 1–5; ConfigShortcutRevealHint misses My blues; Help.Tips.1 stale; empty states uneven (MoonlitPane.cs:514, PlanPane.cs:231 lack reset).
F8 Restored alt/filters confuse returning players.
Proposals: 1 Tour selects a real quest (Must,S); 2 First-run "Set up your road" card (Must,M); 3 Overlay & Nearby in main window + first-pin prompt (Must,S); 4 Help topic "While you play" + HelpActions (Should,S); 5 Settings nav: sections list + search, reorder (Should,M); 6 Cumulative What's new + Highlights (Should,S); 7 Commands: tour/tab/route subcommands, did-you-mean, hide glyphs, README report (Should,S); 8 "Viewing Alt · 3 filters" context bar (Could,S); 9 Consistent empty states (Could,S); 10 clean-ups (Could,S).
Qs: overlay on by default?; setup card vs tour's last chapter; rail-foot buttons ok?; hide /tsuki glyphs?; Highlights line authored or summarised?; chat confirmation for state-changing commands?
