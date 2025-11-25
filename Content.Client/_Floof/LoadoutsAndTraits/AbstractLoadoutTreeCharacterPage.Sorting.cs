namespace Content.Client._Floof.LoadoutsAndTraits;


public abstract partial class AbstractLoadoutTreeCharacterPage<TProto, TCategory, TSelector>
{
    /// <summary>
    ///     Counter number from <see cref="Counters"/> by which to sort items. 0 means default sorting (alphabetic).
    ///     1 or greater means sorting by the specified counter.
    /// </summary>
    protected int SortByCounter = 0;

    /// <summary>
    ///     Returns an item comparator. By default returns a comparer based on the value of <see cref="SortByCounter"/>.
    /// </summary>
    protected virtual Comparison<TProto> GetItemComparison()
    {
        if (SortByCounter == 0)
            return (a, b) => string.Compare(GetLocalizedName(a), GetLocalizedName(b), StringComparison.OrdinalIgnoreCase);

        // Ensure SortByCounter is valid
        SortByCounter = Math.Clamp(SortByCounter, 0, Counters.Count);

        var counter = Counters[SortByCounter - 1];
        return (a, b) =>
        {
            // Sort by the counter first, fall back to sorting by name if counters are equal.
            var result = counter.GetPrototypeCost(a) - counter.GetPrototypeCost(b);
            return result != 0
                ? result
                : string.Compare(GetLocalizedName(a), GetLocalizedName(b), StringComparison.OrdinalIgnoreCase);
        };
    }

    /// <summary>
    ///     Sets <see cref="SortByCounter"/>, ensures it is in range of [0, Counters.size], and updates layout.
    /// </summary>
    protected virtual void SetSortingMode(int sortByCounter)
    {
        SortByCounter = Math.Abs(sortByCounter % (Counters.Count + 1));
        UpdateChoices();

        var choiceName = SortByCounter == 0 ? "null" : Loc.GetString(Counters[SortByCounter - 1].NameLoc);
        Model.SortModeToggleButton.Text = Loc.GetString("loadouts-and-traits-sort-mode-text", ("mode", choiceName));
    }
}
