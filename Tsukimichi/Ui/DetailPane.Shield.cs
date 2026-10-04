using Tsukimichi.Core.Query;

namespace Tsukimichi.Ui;

/// <summary>
/// The detail pane's placeholders (spec-1.20 N6): each hidden name it prints, in the hero, the Giver card, How you'll
/// clear it, Rewards and Unlocks, takes the shield's hover and right-click (<see cref="ShieldText"/>) within the
/// quest's own context, so "Reveal names in this quest" is offered beside "Reveal this name", and reveals the quest's
/// duties with the rest.
/// </summary>
public sealed partial class DetailPane
{
    /// <summary>The hover and right-click of a placeholder that is the last item drawn.</summary>
    /// <param name="kind">The hidden name's kind.</param>
    /// <param name="name">The hidden name itself.</param>
    /// <param name="shown">The placeholder as printed.</param>
    /// <param name="lead">The placeholder over the hover's lines when the slot cut it short; null for none.</param>
    /// <param name="standIn">A duty hidden because the quest is, not by its own name (<see cref="ShieldText.RevealItems"/>).</param>
    private void ShieldItem(SpoilerKind kind, string name, string shown, string? lead = null, bool standIn = false)
    {
        if (shieldSession is { } session)
        {
            ShieldText.InteractItem(session, kind, name, shown, model.Quest, links, lead: lead, duties: model.DutyNames, standIn: standIn);
        }
    }
}
