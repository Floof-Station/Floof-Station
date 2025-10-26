using System.Linq;
using Content.Client.Administration.UI;
using Content.Client.Players.PlayTimeTracking;
using Content.Shared._Floof.LoadoutsAndTraits.Prototypes;
using Content.Shared.Customization.Systems;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;


namespace Content.Client._Floof.LoadoutsAndTraits;

/// <summary>
///     Represents an abstract page of loadout customization menu that uses a tree-like structure and keeps track of character "points".
///
///     This class wraps <see cref="AbstractLoadoutTreeUiModel"/> because RT doesn't support abstract generic classes in the ui.
/// </summary>
public abstract partial class AbstractLoadoutTreeCharacterPage<TProto, TCategory, TSelector> : Control
    where TProto : class, IRecursivePrototype<TCategory, TProto>, IPrototype
    where TCategory : class, IRecursivePrototypeCategory<TCategory, TProto>, IPrototype
    where TSelector : Control
{
    [Dependency] protected readonly IPrototypeManager ProtoMan = default!;
    [Dependency] protected readonly IEntityManager EntMan = default!;
    [Dependency] protected readonly IConfigurationManager Cfg = default!;
    [Dependency] protected readonly ILocalizationManager LocMan = default!;
    [Dependency] private readonly JobRequirementsManager _jobRequirementsManager = default!;

    private CharacterRequirementsSystem? _characterRequirements;
    private JobPrototype? _fallbackJob;
    [ValidatePrototypeId<JobPrototype>] private static ProtoId<JobPrototype> _fallbackJobId = "Passenger";

    /// <summary>
    ///     List of all prototypes relevant to this page.
    /// </summary>
    protected List<TProto> AllPrototypes = new();
    protected List<TCategory> AllCategories = new();
    protected readonly List<CategoryTreeItem> RootCategories = new();
    protected bool PrototypesLoaded;
    protected Dictionary<Button, ConfirmationData> ButtonConfirmationData = new();
    protected readonly ISawmill Log = Logger.GetSawmill($"tree-page-{typeof(TProto)}");

    protected bool LayoutInitialized = false;
    protected bool ShowUnusable = false;
    /// <summary>
    ///     Prototype currently being shown in the details container.
    ///     Null if no prototype is being shown.
    /// </summary>
    protected TProto? ShowingDetailsFor;
    /// <summary>
    ///     Current path of categories. Root category is represented by a "root" entry.
    /// </summary>
    protected readonly Stack<CategoryTreeItem> CurrentPath = new();
    /// <summary>
    ///     Pseudo-category that represents the root of the tree.
    /// </summary>
    protected CategoryTreeItem RootCategory { get; private set; } = new(null, "root", null, null);

    /// <summary>
    ///     List of all point counters relevant to this page. This includes trait points, trait slots, etc.
    ///     Should be initialized in the constructor.
    /// </summary>
    protected readonly List<PointCounterDef> Counters = new();
    public bool CountersValid { get; private set; }

    /// <summary>
    ///     Actual UI wrapped by this model.
    /// </summary>
    public AbstractLoadoutTreeUiModel Model { get; }

    public event Action? OnDirty;

    protected AbstractLoadoutTreeCharacterPage()
    {
        IoCManager.InjectDependencies(this);
        Model = new();
        AddChild(Model);

        EnsurePathIsRooted();

        Model.ShowUnusableButton.OnToggled += args =>
        {
            ShowUnusable = args.Pressed;
            UpdateChoices();
        };
        Model.RemoveUnusableButton.OnPressed += (args) =>
        {
            if (!AdminUIHelpers.TryConfirm(Model.RemoveUnusableButton, ButtonConfirmationData))
                return;

            RemoveUnusable();
        };
        Model.SearchBar.OnTextChanged += _ => UpdateChoices();
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        ProtoMan.PrototypesReloaded += UpdatePrototypes;
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();
        ProtoMan.PrototypesReloaded -= UpdatePrototypes;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        if (Visible)
        {
            if (!PrototypesLoaded)
            {
                UpdatePrototypes(null);
                PrototypesLoaded = true;
            }

            if (!LayoutInitialized)
            {
                InitializeLayout();
                LayoutInitialized = true;
            }
        }

        base.FrameUpdate(args);
    }

    /// <summary>
    ///     Called when prototypes are reloaded and when first shown in the UI.
    ///     Inheritors must call the supermethod or set PrototypesLoaded to true inside it.
    /// </summary>
    protected virtual void UpdatePrototypes(PrototypesReloadedEventArgs? args)
    {
        if (args != null && !args.WasModified<TProto>() && !args.WasModified<TCategory>())
            return;

        AllPrototypes = ProtoMan.EnumeratePrototypes<TProto>().ToList();
        AllCategories = ProtoMan.EnumeratePrototypes<TCategory>().ToList();

        RootCategories.Clear();
        BuildTree(AllCategories.Where(it => it.Root).ToList(), RootCategories);
        RootCategory = new(null, "root", null, RootCategories);

        #if DEBUG
        // Ensure all prototypes are in a category
        foreach (var prototype in AllPrototypes)
        {
            if (AllCategories.None(it => it.SubCategories.Contains(prototype.ID)))
            {
                Log.Error($"Prototype {prototype.ID} is not in any valid category.");
            }
        }
        #endif
    }

    /// <summary>
    ///     Recursively builds the tree of prototypes, outputting the result into <paramref name="outputList"/>.
    /// </summary>
    protected virtual void BuildTree(List<TCategory> rootCategories, List<CategoryTreeItem> outputList)
    {
        foreach (var category in rootCategories)
        {
            CategoryTreeItem categoryTreeItem;

            var locName = GetLocalizedName(category);
            if (category.SubCategories.Count == 0)
            {
                // Terminal node
                categoryTreeItem = new(
                    category,
                    locName,
                    AllPrototypes.Where(it => it.Category == category.ID).ToList(),
                    null);
                outputList.Add(categoryTreeItem);

                if (category.SubCategories.Count > 0)
                    Log.Error($"Category {locName} contains both terminal and non-terminal nodes. Skipping non-terminals.");

                continue;
            }

            // Non-terminal node
            var subcategories = new List<TCategory>();
            foreach (var subCatId in category.SubCategories)
            {
                // Technically the yaml linter should catch that, but...
                if (!ProtoMan.TryIndex(subCatId, out var subCatProto))
                {
                    Log.Error($"Category {locName} references an unknown subcategory {subCatId}");
                    continue;
                }

                subcategories.Add(subCatProto);
            }

            var subCatsList = new List<CategoryTreeItem>();
            BuildTree(subcategories, subCatsList);
            categoryTreeItem = new(
                category,
                locName,
                null,
                subCatsList);

            outputList.Add(categoryTreeItem);
        }
    }

    /// <summary>
    ///     Initializes the layout of the page. This is called when the page is first shown.
    /// </summary>
    protected virtual void InitializeLayout()
    {
        foreach (var counter in Counters)
        {
            PointCounterControl pointCounterControl = new(counter.LocString);
            Model.PointCountersContainer.AddChild(pointCounterControl);
            counter.Control = pointCounterControl;
        }

        UpdatePath();
        UpdateCategories();
        UpdateChoices();
        UpdateDetails();
        UpdateCounters();
    }

    public virtual void UpdateCounters()
    {
        // Reset all counters
        foreach (var counter in Counters)
            counter.CurrentPoints = counter.GetMaxPoints();

        // Recalculate all counters
        foreach (var proto in GetSelected())
        {
            foreach (var counter in Counters)
                counter.CurrentPoints -= counter.GetPrototypeCost(proto);
        }

        // Update controls and validity last
        var valid = true;
        foreach (var counter in Counters)
        {
            valid = valid && counter.Valid; // Using && instead of &= for short-circuiting
            counter.UpdatePoints();
        }
    }

    /// <summary>
    ///     Updates the category path panel.
    /// </summary>
    public virtual void UpdatePath()
    {
        EnsurePathIsRooted();

        var oldPath = Model.PathContainer.Children.Where(it => it is PathButton).Cast<PathButton>().ToArray();
        var newPath = CurrentPath.ToArray();
        // NOTE: since CurrentPath is a stack, newPath is in reverse order. The root category is at the end of the array.
        CategoryTreeItem getNew(int index) => newPath[newPath.Length - index - 1];

        // See if any first entries can be saved, dispose of the rest
        var commonLength = Math.Min(oldPath.Length, newPath.Length);
        var saved = 0;
        int i;
        for (i = 0; i < commonLength; i++)
        {
            if (oldPath[i].Category?.ID != getNew(i).Prototype?.ID)
                break;

            saved++;
        }

        Model.PathContainer.RemoveAllChildren();
        for (i = 0; i < newPath.Length; i++)
        {
            var item = i < saved ? oldPath[i] : null;
            if (item == null)
            {
                var cat = getNew(i).Prototype;
                item = new(cat, cat == null ? string.Empty : GetLocalizedName(cat), i);
                item.OnPressed += _ => GoBackTo(item.Depth);
            }
            Model.PathContainer.AddChild(item);
        }
    }

    protected void EnsurePathIsRooted()
    {
        // Is there a more sane way to do this? Ideally we shouldn't keep that pseudo-category there, but it simplifies the code.
        if (CurrentPath.Count == 0)
            CurrentPath.Push(RootCategory);

        DebugTools.Assert(CurrentPath.FirstOrDefault() == RootCategory, "Path is not rooted");
        DebugTools.Assert(CurrentPath.Count(it => it == RootCategory) == 1, "Path contains bogus roots");
    }

    public virtual void UpdateCategories()
    {
        Model.TabContainer.RemoveAllChildren();

        List<CategoryTreeItem>? categories;
        categories = CurrentPath.Count == 0 ? RootCategories : CurrentPath.Peek().Subcategories;

        if (categories == null)
            return;

        foreach (var category in categories)
        {
            if (category.Prototype is null)
                continue;

            var categoryButton = new CategoryButton(category, GetLocalizedName(category.Prototype));
            categoryButton.OnPressed += _ => GoIntoCategory(category);
            Model.TabContainer.AddChild(categoryButton);
        }
    }

    public virtual void UpdateChoices()
    {
        Model.ChoicesContainer.RemoveAllChildren();

        var currentCategory = CurrentPath.Count > 0
            ? CurrentPath.Peek().Prototypes
            : null;

        if (currentCategory == null)
        {
            Model.ChoicesContainer.AddChild(new Label()
            {
                Text = "This category contains no items." // TODO localize
            });
            return;
        }

        var chosenUnusable = 0;
        foreach (var prototype in currentCategory)
        {
            var usable = IsUsable(prototype, out var reasons);
            var selected = IsSelected(prototype);
            if (!usable)
            {
                if (selected)
                    chosenUnusable++;
                else if (!ShowUnusable)
                    continue;
            }

            if (Model.SearchBar.Text != string.Empty
                && !GetLocalizedName(prototype).Trim().Contains(Model.SearchBar.Text.Trim(), StringComparison.OrdinalIgnoreCase))
                continue;

            var selector = CreateSelector(prototype, usable, selected, reasons);
            Model.ChoicesContainer.AddChild(selector);
        }

        if (Model.ChoicesContainer.ChildCount == 0)
        {
            Model.ChoicesContainer.AddChild(new Label()
            {
                Text = "No items match the requirements." // TODO localize
            });
        }

        // Pretty counterintuitively, this is the place where we update the "remove unusable" button.
        Model.RemoveUnusableButton.Text = Loc.GetString(
            "humanoid-profile-editor-loadouts-remove-unusable-button",
            ("count", chosenUnusable));
        Model.RemoveUnusableButton.Disabled = chosenUnusable == 0;
        AdminUIHelpers.RemoveConfirm(Model.RemoveUnusableButton, ButtonConfirmationData);
    }

    public virtual void UpdateDetails()
    {
        // [X] TODO show why loadout is unusable
        // TODO details panel
        // [X] TODO color loadouts/traits based on unusable/usable/selected-but-unusable
        // TODO sorting (maybe with a mode selector)
        // [X] TODO error: getting 92 unusable loadouts?
        if (ShowingDetailsFor == null)
        {
            Model.DetailsContainer.Visible = false;
            return;
        }

        Model.DetailsContainer.Visible = true;
        Model.DetailsContainer.RemoveAllChildren();
        UpdateExtendedPanel();
    }

    public virtual void RemoveUnusable()
    {
        var unset = 0;
        foreach (var prototype in AllPrototypes)
        {
            if (!IsSelected(prototype) || IsUsable(prototype, out var reasons))
                continue;

            SetSelected(prototype, false);
            unset++;
        }

        Log.Info($"Deselected {unset} items.");
    }

    /// <summary>
    ///     Helper function for checking whether requirements do pass, because upstream never bothered to create one. EE is a shitcode pile.
    /// </summary>
    protected bool CheckRequirementsValid(
        HumanoidCharacterProfile? profile,
        JobPrototype? highJob,
        TProto checkedProto,
        List<CharacterRequirement> requirements,
        out List<string> failReasons)
    {
        _characterRequirements ??= IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<CharacterRequirementsSystem>();
        _fallbackJob ??= ProtoMan.Index(_fallbackJobId);

        var playtimes = _jobRequirementsManager.GetPlayTimes();
        // EE used to create a new empty JobPrototype here, which would cause certain checks to fail. I have no words.
        profile ??= HumanoidCharacterProfile.DefaultWithSpecies();
        highJob ??= _fallbackJob;

        // Also for some reason it requires both prototype and prototype.Requirements? I guess EE never heard of interfaces, inheritance and polymorphism, huh.
        // TODO: can we make this not return false if a loadout has a CIG that conflicts with itself?
        return _characterRequirements.CheckRequirementsValid(
            requirements, highJob, profile, playtimes,
            _jobRequirementsManager.IsWhitelisted(), checkedProto,
            EntMan, ProtoMan, Cfg,
            out failReasons);
    }

    /// <summary>
    ///     Should be called by inheritors when/if the user wants to expand the given item.
    /// </summary>
    public void Expand(TProto? prototype)
    {
        ShowingDetailsFor = prototype;
        UpdateDetails();
    }

    /// <summary>
    ///     Appends a category to the current path. Does not validate the resulting path.
    /// </summary>
    /// <param name="category"></param>
    public void GoIntoCategory(CategoryTreeItem category)
    {
        if (category.Prototype == null)
            return;

        ShowingDetailsFor = null;
        CurrentPath.Push(category);
        UpdateAll();
    }

    /// <summary>
    ///     Returns to the given category depth in the path. If depth is 1 or less, returns to the root.
    /// </summary>
    public void GoBackTo(int depth)
    {
        EnsurePathIsRooted();

        ShowingDetailsFor = null;
        while (CurrentPath.Count > depth + 1) // I won't lie, I have no idea why the +1 is required, but without it we always return 1 too far
            CurrentPath.Pop();

        UpdateAll();
    }

    public void UpdateAll()
    {
        UpdatePath();
        UpdateCategories();
        UpdateChoices();
        UpdateDetails();
        UpdateCounters();
    }

    /// <summary>
    ///     Should be called primarily by inheritors when their state changes and the parent (usually the humanoid profile editor) needs to be informed.
    /// </summary>
    public void Dirty()
    {
        OnDirty?.Invoke();
    }

    /// <summary>
    ///     Creates a selector for the given prototype.
    /// </summary>
    /// <param name="prototype"></param>
    /// <param name="usable">Whether the provided loadout passes the requirements (cached result of IsUsable). Note that this may be false if the loadout is conflicting with itself.</param>
    /// <param name="chosen">Whether this loadout is currently chosen (cached result of IsSelected).</param>
    /// <param name="reasons">The reasons why the loadout is not usable (empty if usable)</param>
    public abstract TSelector CreateSelector(TProto prototype, bool usable, bool chosen, List<string> reasons);

    /// <summary>
    ///     Called when the user tries to view the extended detail/settings of a prototype.
    ///     Should update DetailsContainer (note that it will usually be cleared - though not disposed of - before calling this).
    /// </summary>
    protected abstract void UpdateExtendedPanel();

    /// <summary>
    ///     Checks if the player can use the provided loadout, regardless of points.
    /// </summary>
    public abstract bool IsUsable(TProto prototype, out List<string> reasons);

    public abstract bool IsSelected(TProto prototype);

    public abstract IEnumerable<TProto> GetSelected();

    public abstract void SetSelected(TProto prototype, bool selected);

    public abstract string GetLocalizedName(TCategory prototype);

    public abstract string GetLocalizedName(TProto prototype);

    public abstract string GetLocalizedDescription(TProto prototype);

    public record class PointCounterDef(string LocString, Func<TProto, int> GetPrototypeCost, Func<int> GetMaxPoints)
    {
        public int CurrentPoints { get; internal set; } = int.MinValue;
        public int MaxPoints { get; internal set; } = int.MinValue;

        public bool Valid =>
            MaxPoints != int.MinValue
            && CurrentPoints != int.MinValue
            && (CurrentPoints >= 0 || MaxPoints == int.MaxValue);

        internal PointCounterControl? Control;

        public void UpdatePoints()
        {
            if (Control == null)
                return;

            Control.SetValue(CurrentPoints);
            Control.SetMax(MaxPoints);
        }
    }

    /// <summary>
    ///     An item of the prototype tree. It can either be terminal and contain prototypes, or be non-terminal and contain subcategories.
    ///     It's possible for it to be both. The category field can only be null for the root category.
    /// </summary>
    public record class CategoryTreeItem(TCategory? Prototype, string LocalizedName, List<TProto>? Prototypes, List<CategoryTreeItem>? Subcategories)
    {
        public bool Terminal => Prototypes != null;
        public bool NonTerminal => Subcategories != null;
    }

    public sealed class PathButton : Button
    {
        public TCategory? Category { get; }
        public string LocalizedName { get; }
        public int Depth { get; }

        public readonly TextureRect? Image;

        public PathButton(TCategory? category, string localizedName, int depth)
        {
            Category = category;
            LocalizedName = localizedName;
            Depth = depth;

            HorizontalAlignment = HAlignment.Stretch;

            var isRoot = category == null;
            // Root category is represented by a home sign. Non-root ones are text
            if (isRoot)
            {
                Text = null;
                Image = new()
                {
                    TexturePath = "/Textures/Interface/home.png",
                    SetSize = new(20),
                    HorizontalAlignment = HAlignment.Left,
                };
                AddChild(Image);
            }
            else
            {
                Text = localizedName;
                Image = null;
            }
        }
    }

    public sealed class CategoryButton : Button
    {
        public CategoryTreeItem Category { get; }
        public string LocalizedName { get; }

        public CategoryButton(CategoryTreeItem category, string localizedName)
        {
            Category = category;
            LocalizedName = localizedName;

            Text = localizedName;
            StyleClasses.Add("OpenBoth");
        }
    }
}
