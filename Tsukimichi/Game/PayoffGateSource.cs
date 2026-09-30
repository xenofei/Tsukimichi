using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Payoff;
using Tsukimichi.GameData;

namespace Tsukimichi.Game;

/// <summary>
/// "Before you continue" payoff gates (P5) for the session: the curated gates resolved once per catalog, and the ones
/// that speak for the viewed character (the Characters dashboard and the Tonight card) and for the logged-in one (the
/// chat notice), each memoized on what it depends on. Framework thread only, like the session.
/// </summary>
public sealed class PayoffGateSource
{
    private readonly SessionState session;
    private readonly IPluginLog log;

    private CatalogBundle? builtBundle;
    private PayoffGates gates = PayoffGates.Empty;

    private int viewedVersion = -1;
    private CatalogBundle? viewedBundle;
    private IReadOnlyList<ActivePayoffGate> viewed = [];

    private IReadOnlyDictionary<uint, QuestEvaluation>? liveStates;
    private CatalogBundle? liveBundle;
    private IReadOnlyList<ActivePayoffGate> live = [];

    public PayoffGateSource(SessionState session, IPluginLog log)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>The gates speaking for the viewed character now; empty without a catalog or evaluations.</summary>
    public IReadOnlyList<ActivePayoffGate> Viewed()
    {
        if (session.Bundle is not { } bundle)
        {
            return [];
        }

        if (viewedVersion != session.Version || !ReferenceEquals(viewedBundle, bundle))
        {
            viewedVersion = session.Version;
            viewedBundle = bundle;
            viewed = Compute(bundle, session.States);
        }

        return viewed;
    }

    /// <summary>The gates speaking for the logged-in character now; empty when logged out or before the first pass.</summary>
    public IReadOnlyList<ActivePayoffGate> Live()
    {
        if (session.Bundle is not { } bundle || session.LiveContentId is null)
        {
            return [];
        }

        var states = session.LiveStates;
        if (!ReferenceEquals(states, liveStates) || !ReferenceEquals(liveBundle, bundle))
        {
            liveStates = states;
            liveBundle = bundle;
            live = Compute(bundle, states);
        }

        return live;
    }

    private IReadOnlyList<ActivePayoffGate> Compute(CatalogBundle bundle, IReadOnlyDictionary<uint, QuestEvaluation> states)
    {
        try
        {
            return For(bundle).Active(states);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Payoff gates could not be evaluated");
            return [];
        }
    }

    private PayoffGates For(CatalogBundle bundle)
    {
        if (ReferenceEquals(builtBundle, bundle))
        {
            return gates;
        }

        builtBundle = bundle;
        gates = PayoffGates.Build(bundle.Catalog, session.Curated);
        foreach (var warning in gates.Warnings)
        {
            log.Warning("Payoff gates: {Warning}", warning);
        }

        return gates;
    }
}
