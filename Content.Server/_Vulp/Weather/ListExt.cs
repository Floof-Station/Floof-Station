using System.Linq;
using Robust.Shared.Random;
using Robust.Shared.Utility;


namespace Content.Server._Vulp.Weather;


public static class ListExt
{
    /// <summary>
    ///     Selects a random value from entries based on the weights provided by the weight selector.
    /// </summary>
    public static T WeightedRandom<T>(this List<T> entries, IRobustRandom random, Func<T, float> weightSelector)
    {
        DebugTools.Assert(entries.Count > 0);
        var sum = entries.Sum(weightSelector);
        var target = random.NextFloat(sum);

        foreach (var entry in entries)
        {
            target -= weightSelector(entry);
            if (target <= 0f)
                return entry;
        }

        return entries.Last();
    }
}
