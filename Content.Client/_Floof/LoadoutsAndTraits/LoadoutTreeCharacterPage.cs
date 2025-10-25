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


namespace Content.Client._Floof.LoadoutsAndTraits;


public sealed class LoadoutTreeCharacterPage : AbstractLoadoutTreeCharacterPage<LoadoutPrototype, LoadoutCategoryPrototype, LoadoutSelector2>
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly JobRequirementsManager _jobRequirementsManager = default!;
    private readonly CharacterRequirementsSystem _characterRequirements;

    public readonly Dictionary<ProtoId<LoadoutPrototype>, LoadoutPreference> Preferences = new();
    public int MaxPoints { get; private set; }

    public event Action<HashSet<LoadoutPreference>>? OnPreferencesChanged = null;

    private Func<JobPrototype> _highJobProvider;
    private Func<HumanoidCharacterProfile> _profileProvider;

    public LoadoutTreeCharacterPage(Func<JobPrototype> highJobProvider, Func<HumanoidCharacterProfile> profileProvider) : base()
    {
        IoCManager.InjectDependencies(this);
        _cfg.OnValueChanged(CCVars.GameLoadoutsPoints, OnMaxPointsChanged, true);
        _characterRequirements = _entityManager.System<CharacterRequirementsSystem>();

        _highJobProvider = highJobProvider;
        _profileProvider = profileProvider;

        Counters.Add(new("loadout-point-counter", proto => proto.Cost, () => MaxPoints));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;

        _cfg.UnsubValueChanged(CCVars.GameLoadoutsPoints, OnMaxPointsChanged);
    }

    private void OnMaxPointsChanged(int obj)
    {
        MaxPoints = obj;
        UpdateCounters();
    }

    public override LoadoutSelector2 CreateSelector(LoadoutPrototype prototype)
    {
        return new(this, GetOrNew(prototype.ID), prototype);
    }

    protected override void UpdateExtendedPanel()
    {
        // TODO
    }

    public override bool IsUsable(LoadoutPrototype prototype, out List<string> reasons)
    {
        var playtimes = _jobRequirementsManager.GetPlayTimes();
        var usable = _characterRequirements.CheckRequirementsValid(
            prototype.Requirements, _highJobProvider(), _profileProvider(), playtimes,
            _jobRequirementsManager.IsWhitelisted(), prototype,
            _entityManager, _prototypeManager, _cfg,
            out reasons);

        return usable;
    }

    public override bool IsSelected(LoadoutPrototype prototype)
    {
        if (!Preferences.TryGetValue(prototype.ID, out var preference))
            return false;

        return preference.Selected;
    }

    public override void SetSelected(LoadoutPrototype prototype, bool selected)
    {
        var preference = GetOrNew(prototype.ID);
        preference.Selected = selected;
    }

    public override string GetLocalizedName(LoadoutCategoryPrototype prototype) =>
        Loc.GetString($"loadout-category-{prototype.ID}");

    public override string GetLocalizedName(LoadoutPrototype prototype) =>
        Loc.GetString($"loadout-name-{prototype.ID}");

    public LoadoutPreference GetOrNew(ProtoId<LoadoutPrototype> proto) => Preferences.GetOrNew(proto);
}
