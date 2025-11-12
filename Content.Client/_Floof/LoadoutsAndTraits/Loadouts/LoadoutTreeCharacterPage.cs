using System.Linq;
using Content.Client.Players.PlayTimeTracking;
using Content.Shared.CCVar;
using Content.Shared.Clothing.Loadouts.Prototypes;
using Content.Shared.Clothing.Loadouts.Systems;
using Content.Shared.Customization.Systems;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;


namespace Content.Client._Floof.LoadoutsAndTraits.Loadouts;


public sealed class LoadoutTreeCharacterPage : AbstractLoadoutTreeCharacterPage<LoadoutPrototype, LoadoutCategoryPrototype, LoadoutSelector2>
{
    public readonly Dictionary<ProtoId<LoadoutPrototype>, LoadoutPreference> Preferences = new();
    public int MaxPoints { get; private set; }

    private Func<JobPrototype> _highJobProvider;
    private Func<HumanoidCharacterProfile> _profileProvider;

    public LoadoutTreeCharacterPage(Func<JobPrototype> highJobProvider, Func<HumanoidCharacterProfile> profileProvider) : base()
    {
        Cfg.OnValueChanged(CCVars.GameLoadoutsPoints, OnMaxPointsChanged, true);

        _highJobProvider = highJobProvider;
        _profileProvider = profileProvider;

        Counters.Add(new("loadout-point-counter", proto => proto.Cost, () => MaxPoints));
    }

    ~LoadoutTreeCharacterPage()
    {
        Cfg.UnsubValueChanged(CCVars.GameLoadoutsPoints, OnMaxPointsChanged);
    }

    private void OnMaxPointsChanged(int obj)
    {
        MaxPoints = obj;
        UpdateCounters();
    }

    public override LoadoutSelector2 CreateSelector(LoadoutPrototype prototype) => new(this, GetOrNew(prototype.ID), prototype);

    protected override void UpdateDetails(LoadoutPrototype subject)
    {
        base.UpdateDetails(subject);

        var extendedInfo = new LoadoutExtendedInfo2(this, GetOrNew(subject.ID), subject);
        extendedInfo.OnPreferencesChanged += () => Dirty();
        Model.DetailsContainer.AddChild(extendedInfo);
    }

    public override bool IsUsable(LoadoutPrototype prototype, out List<string> reasons)
    {
        return CheckRequirementsValid(_profileProvider(), _highJobProvider(), prototype, prototype.Requirements, out reasons);
    }

    public override bool IsSelected(LoadoutPrototype prototype)
    {
        if (!Preferences.TryGetValue(prototype.ID, out var preference))
            return false;

        return preference.Selected;
    }

    public override IEnumerable<LoadoutPrototype> GetSelected()
    {
        foreach (var pref in Preferences.Values)
        {
            if (!pref.Selected || !ProtoMan.TryIndex<LoadoutPrototype>(pref.LoadoutName, out var prototype))
                continue;

            yield return prototype;
        }
    }

    public override void SetSelected(LoadoutPrototype prototype, bool selected)
    {
        var preference = GetOrNew(prototype.ID);
        preference.Selected = selected;
        Dirty();
        UpdateSpecials();
        UpdateChoices(); // This may invalidate other options due to loadout groups
        UpdateCounters();
    }

    public override string GetLocalizedName(LoadoutCategoryPrototype prototype) =>
        LocMan.GetString($"loadout-category-{prototype.ID}");

    // Note: we do not account for custom names because those are set by the user and can change by runtime, but sorting depends on them being stable.
    public override string GetLocalizedName(LoadoutPrototype prototype) =>
        LocMan.TryGetString($"loadout-name-{prototype.ID}", out var customName) ? customName
        : GetItemName(prototype);

    private string GetItemName(LoadoutPrototype prototype)
    {
        if (prototype.Items.Count == 0 || !ProtoMan.TryIndex(prototype.Items[0], out var itemProto))
            return "???";

        return itemProto.Name;
    }

    public override string GetLocalizedDescription(LoadoutPrototype prototype) =>
        // Try custom description first
        Preferences.TryGetValue(prototype, out var pref) && pref.CustomDescription != null ? pref.CustomDescription
        // Fall back to prototype-specific description
        : LocMan.TryGetString($"loadout-description-{prototype.ID}", out var customDesc) ? customDesc
        // Fall back to the description of the item provided by the loadout
        : GetItemDescription(prototype);

    private string GetItemDescription(LoadoutPrototype prototype)
    {
        if (prototype.Items.Count == 0 || !ProtoMan.TryIndex(prototype.Items[0], out var itemProto))
            return "???";

        return itemProto.Description;
    }

    public LoadoutPreference GetOrNew(ProtoId<LoadoutPrototype> proto)
    {
        if (!Preferences.TryGetValue(proto, out var preference))
            Preferences[proto] = preference = new(proto.Id);

        return preference;
    }
}
