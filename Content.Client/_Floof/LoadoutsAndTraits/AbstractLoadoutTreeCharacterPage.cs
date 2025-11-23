using System.Diagnostics;
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
    where TSelector : AbstractLoadoutSelector
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
    protected readonly ISawmill Log = Logger.GetSawmill($"tree-page-{typeof(TProto).Name}");

    protected bool LayoutInitialized = false;
    protected bool ShowUnusable = false;
    /// <summary>
    ///     Prototype currently being shown in the details container.
    ///     Null if no prototype is being shown.
    /// </summary>
    protected TProto? ShowingExtendedInfoFor;
    /// <summary>
    ///     Current path of categories. Root category is represented by a "root" entry.
    /// </summary>
    protected readonly Stack<CategoryTreeItem> CurrentPath = new();
    public CategoryTreeItem CurrentCategory => CurrentPath.Count == 0 ? RootCategory : CurrentPath.Peek();

    /// <summary>
    ///     Pseudo-category that represents the root of the tree.
    /// </summary>
    protected readonly CategoryTreeItem RootCategory;
    /// <summary>
    ///     Pseudo-category that represents the "chosen items" section.
    /// </summary>
    protected readonly CategoryTreeItem ChosenItemsCategory;

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
        HorizontalExpand = true;
        AddChild(Model);

        RootCategory = new(null, "__root__", null, RootCategories);
        ChosenItemsCategory = new(null, Loc.GetString("loadouts-and-traits-chosen-items"), new(), null);
        CurrentPath.Push(RootCategory);
        SetSortingMode(0); // Just so we update the button's text

        Model.ShowUnusableButton.OnToggled += args =>
        {
            ShowUnusable = args.Pressed;
            UpdateChoices();
        };
        Model.RemoveUnusableButton.OnPressed += _ =>
        {
            if (!AdminUIHelpers.TryConfirm(Model.RemoveUnusableButton, ButtonConfirmationData))
                return;

            RemoveUnusable();
        };
        Model.SortModeToggleButton.OnPressed += _ => { SetSortingMode(SortByCounter + 1); };
        Model.SearchBar.OnTextChanged += _ => UpdateChoices();
    }

    [MustCallBase]
    protected override void EnteredTree()
    {
        base.EnteredTree();
        ProtoMan.PrototypesReloaded += UpdatePrototypes;
    }

    [MustCallBase]
    protected override void ExitedTree()
    {
        base.ExitedTree();
        ProtoMan.PrototypesReloaded -= UpdatePrototypes;
    }

    [MustCallBase]
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
    [MustCallBase]
    protected virtual void UpdatePrototypes(PrototypesReloadedEventArgs? args)
    {
        if (args != null && !args.WasModified<TProto>() && !args.WasModified<TCategory>())
            return;

        AllPrototypes = ProtoMan.EnumeratePrototypes<TProto>().ToList();
        AllCategories = ProtoMan.EnumeratePrototypes<TCategory>().ToList();

        RootCategories.Clear();
        BuildTree(AllCategories.Where(it => it.Root).ToList(), RootCategories);
        #if DEBUG
        ValidatePrototypes();
        #endif

        // Snowflake case - the chosen items pseudo-category
        RootCategories.Add(ChosenItemsCategory);
    }

    private void ValidatePrototypes()
    {
        // Ensure all prototypes are in a category
        List<TProto> prototypesNotInCategory = AllPrototypes.ToList();
        void RecCheck(CategoryTreeItem item)
        {
            if (item.Prototypes != null)
                prototypesNotInCategory.RemoveAll(it => item.Prototypes.Contains(it));

            if (item.Subcategories != null)
                foreach (var subcategory in item.Subcategories)
                    RecCheck(subcategory);
        }

        RecCheck(RootCategory);

        if (prototypesNotInCategory.Count > 0)
            Log.Error($"The following prototypes are not in any valid category reachable from the root: {string.Join(", ", prototypesNotInCategory.Select(it => it.ID))}");
    }

    /// <summary>
    ///     Recursively builds the tree of prototypes, outputting the result into <paramref name="outputList"/>.
    /// </summary>
    [MustCallBase]
    protected virtual void BuildTree(List<TCategory> rootCategories, List<CategoryTreeItem> outputList)
    {
        foreach (var category in rootCategories)
        {
            CategoryTreeItem categoryTreeItem;

            var locName = GetLocalizedName(category);
            if (category.SubCategories.Count == 0)
            {
                // Terminal node
                var protos = AllPrototypes.Where(it => it.Category == category.ID).ToList();
                // Note: we don't sort prototypes here
                categoryTreeItem = new(
                    category,
                    locName,
                    protos,
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
            // Sorting subcategories by ID because that's how EE intended it. They apparently never learned they can just add an "order" field.
            subcategories.Sort((a, b) => string.Compare(a.ID, b.ID, StringComparison.Ordinal));

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
    [MustCallBase]
    protected virtual void InitializeLayout()
    {
        foreach (var counter in Counters)
        {
            PointCounterControl pointCounterControl = new(counter.DescLoc);
            Model.PointCountersContainer.AddChild(pointCounterControl);
            counter.Control = pointCounterControl;
        }

        UpdateAll();
    }

    [MustCallBase]
    public virtual void UpdateCounters()
    {
        // Reset all counters
        foreach (var counter in Counters)
            counter.MaxPoints = counter.CurrentPoints = counter.GetMaxPoints();

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

        CountersValid = valid;
        Model.OutOfPointsLabel.Visible = !valid;

        // Update unusable counter
        var chosenUnusable = 0;
        foreach (var prototype in GetSelected())
        {
            if (!IsUsable(prototype, out var reasons))
                chosenUnusable++;
        }

        Model.RemoveUnusableButton.Text = Loc.GetString(
            "humanoid-profile-editor-loadouts-remove-unusable-button",
            ("count", chosenUnusable));
        Model.RemoveUnusableButton.Disabled = chosenUnusable == 0;
        AdminUIHelpers.RemoveConfirm(Model.RemoveUnusableButton, ButtonConfirmationData);
    }

    /// <summary>
    ///     Updates the category path panel.
    /// </summary>
    [MustCallBase]
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
                var category = getNew(i);
                var proto = category.Prototype;
                item = new(proto, proto == null ? string.Empty : GetLocalizedName(proto), i);
                item.OnPressed += _ => ReturnToCategory(category);
            }
            Model.PathContainer.AddChild(item);
        }
    }

    protected void EnsurePathIsRooted()
    {
        if (CurrentPath.Count == 0)
            CurrentPath.Push(RootCategory);

        DebugTools.Assert(CurrentPath.Last() == RootCategory, "Path is not rooted"); // Note: this is a stack, so last == bottom == root
        DebugTools.Assert(CurrentPath.Count(it => it == RootCategory) == 1, "Path contains bogus roots");
    }

    /// <summary>
    ///     Updates special categories like the "chosen items" section.
    /// </summary>
    [MustCallBase]
    public virtual void UpdateSpecials()
    {
        var chosen = ChosenItemsCategory.Prototypes;
        DebugTools.Assert(chosen != null);
        chosen!.Clear();
        chosen!.AddRange(GetSelected());
    }

    [MustCallBase]
    public virtual void UpdateCategories()
    {
        Model.TabContainer.RemoveAllChildren();

        var categories = CurrentCategory.Subcategories;
        if (categories == null)
            return;

        foreach (var category in categories)
        {
            if (category.Empty)
                continue;

            var categoryButton = new CategoryButton(category, category.LocalizedName);
            categoryButton.OnPressed += _ => EnterCategory(category);
            Model.TabContainer.AddChild(categoryButton);
        }
    }

    [MustCallBase]
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

        currentCategory.Sort(GetItemComparison());
        foreach (var prototype in currentCategory)
        {
            var usable = IsUsable(prototype, out var reasons);
            var selected = IsSelected(prototype);
            if (!usable && !ShowUnusable)
                continue;

            if (Model.SearchBar.Text != string.Empty
                && !GetLocalizedName(prototype).Trim().Contains(Model.SearchBar.Text.Trim(), StringComparison.OrdinalIgnoreCase))
                continue;

            var selector = CreateSelector(prototype);
            selector.InferStyleFromState(!usable, selected, reasons);
            Model.ChoicesContainer.AddChild(selector);
        }

        if (Model.ChoicesContainer.ChildCount == 0)
        {
            Model.ChoicesContainer.AddChild(new Label()
            {
                Text = "No items match the requirements." // TODO localize
            });
        }
    }

    public void UpdateExtendedInfo()
    {
        Model.DetailsContainer.RemoveAllChildren();
        if (ShowingExtendedInfoFor != null && CurrentCategory.Prototypes?.Contains(ShowingExtendedInfoFor) != true)
            ShowingExtendedInfoFor = null; // Close the panel if the item is no longer in the current category

        if (ShowingExtendedInfoFor == null)
        {
            Model.DetailsContainer.Visible = false;
            return;
        }

        Model.DetailsContainer.Visible = true;
        UpdateDetails(ShowingExtendedInfoFor);
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
        _characterRequirements ??= IoCManager.Resolve<IEntitySystemManager>().GetEntitySystemOrNull<CharacterRequirementsSystem>();
        if (_characterRequirements == null)
        {
            // IOC my behatred. Ideally this method shouldn't be called mid-connection, but sometimes it can, in which case the system will fail to resolve.
            failReasons = new();
            return true;
        }
        _fallbackJob ??= ProtoMan.Index(_fallbackJobId);

        // !!! landmine: make sure to use GetRawPlayTime trackers, as this is what the loadout system was made to rely on.
        // GetPlayTimes would return a Dictionary<ProtoId<Job>, TimeSpan>, whereas this returns a Dictionary<ProtoId<PlayTimeTracker>, TimeSpan>, but neither of them are typed properly
        var playtimes = _jobRequirementsManager.GetRawPlayTimeTrackers();
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
        ShowingExtendedInfoFor = ShowingExtendedInfoFor != prototype ? prototype : null; // Toggle if the button is pressed twice
        UpdateExtendedInfo();
    }

    /// <summary>
    ///     Appends a category to the current path. Does not validate the resulting path.
    /// </summary>
    /// <param name="category"></param>
    public void EnterCategory(CategoryTreeItem category)
    {
        if (category == RootCategory)
            return; // Just in case

        ShowingExtendedInfoFor = null;
        CurrentPath.Push(category);
        UpdateAll();
    }

    /// <summary>
    ///     Returns to the given category depth in the path. If the category is not in the path, returns to root.
    /// </summary>
    public void ReturnToCategory(CategoryTreeItem? category)
    {
        ShowingExtendedInfoFor = null;
        while (CurrentPath.Count > 0 && CurrentCategory != category)
            CurrentPath.Pop();

        UpdateAll();
    }

    public void UpdateAll()
    {
        UpdatePath();
        UpdateSpecials();
        UpdateCategories();
        UpdateChoices();
        UpdateExtendedInfo();
        UpdateCounters();
    }

    /// <summary>
    ///     Should be called primarily by inheritors when their state changes and the parent (usually the humanoid profile editor) needs to be informed.
    /// </summary>
    public void Dirty()
    {
        OnDirty?.Invoke();

        if (ShowingExtendedInfoFor is { } infoItem && !IsSelected(infoItem))
            UpdateExtendedInfo();
    }

    /// <summary>
    ///     Creates a selector for the given prototype.
    /// </summary>
    public abstract TSelector CreateSelector(TProto prototype);

    /// <summary>
    ///     Called when the user tries to view the extended detail/settings of a prototype, should be overridden by inheritors.
    ///     Should update DetailsContainer (note that it will usually be cleared - though not disposed of - before calling this).
    /// </summary>
    /// <param name="subject"></param>
    [MustCallBase]
    protected virtual void UpdateDetails(TProto subject)
    {
        Model.DetailsContainer.AddChild(new RichTextLabel()
        {
            Text = $"Details for {GetLocalizedName(ShowingExtendedInfoFor!)}.",
            StyleClasses = { "LabelHeading" },
            Margin = new(2f, 2f, 2f, 10f)
        });
    }

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

    public record class PointCounterDef(string NameLoc, string DescLoc, Func<TProto, int> GetPrototypeCost, Func<int> GetMaxPoints)
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
        public bool Empty => (Prototypes == null || Prototypes?.Count == 0) && (Subcategories == null || Subcategories?.Count == 0);
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
                // Special categories get a special blue font
                Label.FontColorOverride = Category == null ? new(128, 128, 255) : null;
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
