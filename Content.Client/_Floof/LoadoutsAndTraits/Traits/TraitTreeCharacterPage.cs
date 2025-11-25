using Content.Shared._Floof.LoadoutsAndTraits.Data;
using Content.Shared.CCVar;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Traits;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;


namespace Content.Client._Floof.LoadoutsAndTraits.Traits;


public sealed class TraitTreeCharacterPage : AbstractLoadoutTreeCharacterPage<TraitPrototype, TraitCategoryPrototype, TraitSelector2>
{
    public readonly Dictionary<ProtoId<TraitPrototype>, TraitPreference> Preferences = new();
    public int MaxPoints { get; private set; }
    public int MaxSelections { get; private set; }

    private Func<JobPrototype> _highJobProvider;
    private Func<HumanoidCharacterProfile> _profileProvider;

    public TraitTreeCharacterPage(Func<JobPrototype> highJobProvider, Func<HumanoidCharacterProfile> profileProvider)
    {
        Cfg.OnValueChanged(CCVars.GameTraitsDefaultPoints, OnMaxPointsChanged, true);
        Cfg.OnValueChanged(CCVars.GameTraitsMax, OnMaxSelectionsChanged, true);

        _highJobProvider = highJobProvider;
        _profileProvider = profileProvider;

        // This weird system expresses loadout cost as "how many points this takes", but trait cost as "how many points this GIVES"
        // 0. fucking. consistency.
        Counters.Add(new("loadout-point-counter-name", "loadout-point-counter", proto => -proto.Points, () => MaxPoints));
        Counters.Add(new("loadout-selection-counter-name", "loadout-selection-counter", proto => proto.Slots, () => MaxSelections));
        UpdateCounters();
    }

    ~TraitTreeCharacterPage()
    {
        Cfg.UnsubValueChanged(CCVars.GameTraitsDefaultPoints, OnMaxPointsChanged);
        Cfg.UnsubValueChanged(CCVars.GameTraitsMax, OnMaxSelectionsChanged);
    }

    private void OnMaxPointsChanged(int value)
    {
        MaxPoints = value;
        UpdateCounters();
    }

    private void OnMaxSelectionsChanged(int value)
    {
        MaxSelections = value;
        UpdateCounters();
    }

    public override TraitSelector2 CreateSelector(TraitPrototype prototype)
    {
        var selector = new TraitSelector2(this, GetOrNew(prototype.ID), prototype);
        return selector;
    }

    protected override void UpdateDetails(TraitPrototype prototype)
    {
        base.UpdateDetails(prototype);

        // Description label as there's no per-trait options yet
        Model.DetailsContainer.AddChild(new RichTextLabel()
        {
            Text = GetLocalizedDescription(prototype),
            HorizontalExpand = true
        });
    }

    public override bool IsUsable(TraitPrototype prototype, out List<string> reasons)
    {
        return CheckRequirementsValid(_profileProvider(), _highJobProvider(), prototype, prototype.Requirements, out reasons);
    }

    public override bool IsSelected(TraitPrototype prototype)
    {
        if (!Preferences.TryGetValue(prototype.ID, out var preference))
            return false;

        return preference.Selected;
    }

    public override IEnumerable<TraitPrototype> GetSelected()
    {
        foreach (var pref in Preferences.Values)
        {
            if (!pref.Selected || !ProtoMan.TryIndex(pref.Prototype, out var prototype))
                continue;

            yield return prototype;
        }
    }

    public override void SetSelected(TraitPrototype prototype, bool selected)
    {
        var preference = GetOrNew(prototype.ID);
        preference.Selected = selected;
        Dirty();
        UpdateSpecials();
        UpdateChoices(); // This may invalidate other options due to loadout groups
        UpdateCounters();
    }

    public override string GetLocalizedName(TraitCategoryPrototype prototype) =>
        Loc.GetString($"trait-category-{prototype.ID}");

    public override string GetLocalizedName(TraitPrototype prototype) =>
        Loc.GetString($"trait-name-{prototype.ID}");

    public override string GetLocalizedDescription(TraitPrototype prototype) =>
        Loc.GetString($"trait-description-{prototype.ID}");

    public TraitPreference GetOrNew(ProtoId<TraitPrototype> proto)
    {
        if (!Preferences.TryGetValue(proto, out var preference))
            Preferences[proto] = preference = new(proto.Id, false);

        return preference;
    }
}
