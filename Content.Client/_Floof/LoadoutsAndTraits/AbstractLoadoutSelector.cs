using System.Text;
using Content.Client.Stylesheets;
using Content.Shared.Clothing.Loadouts.Systems;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Utility;


namespace Content.Client._Floof.LoadoutsAndTraits;


/// <summary>
///     Loadout selector that provides a preference button which supports the following states:
///     - Selected (pressed green)
///     - Unselected usable (not pressed, gray)
///     - Unselected unusable (error, red)
///     - Selected unusable (warning, yellow)
/// </summary>
public abstract class AbstractLoadoutSelector : Control
{
    protected abstract BaseButton PreferenceButtonRef { get; }
    protected abstract string Description { get; }

    protected string
        NormalSelectorClass = ContainerButton.StyleClassButton,
        UnusableSelectorClass = StyleBase.ButtonDanger,
        SelectedUnusableSelectorClass = StyleBase.ButtonCaution;

    /// <summary>
    ///     Infers and applies the style of the preference button on the state of the loadout.
    /// </summary>
    /// <param name="unusable">Whether the loadout is unusable.</param>
    /// <param name="selected">Whether the loadout is currently selected.</param>
    /// <param name="reasons">If unusable, the list of reasons why this loadout is unusable.</param>
    public virtual void InferStyleFromState(bool unusable, bool selected, List<string> reasons)
    {
        // Apply style
        PreferenceButtonRef.StyleClasses.Add(NormalSelectorClass);
        PreferenceButtonRef.StyleClasses.Remove(SelectedUnusableSelectorClass);
        PreferenceButtonRef.StyleClasses.Remove(UnusableSelectorClass);
        if (unusable)
            PreferenceButtonRef.StyleClasses.Add(selected ? SelectedUnusableSelectorClass : UnusableSelectorClass);

        // Add tooltip if applicable
        PreferenceButtonRef.TooltipSupplier = _ => GetTooltip(unusable, reasons);
    }

    private Tooltip? GetTooltip(bool unusable, List<string> reasons)
    {
        // Unlike EE, we create the tooltip dynamically, when it's needed.
        var tooltip = new StringBuilder();
        // Description comes first
        // NOTE: StringBuilder.AppendLine is a sandbox violation, but StringBuilder.Append is not
        if (Description is { Length: >0 } description)
        {
            tooltip.Append(description + "\n\n");
        }

        // Add requirement reasons to the tooltip, but only if it's considered unusable.
        if (unusable)
            foreach (var reason in reasons)
                tooltip.Append($"{reason}\n");

        if (tooltip.Length <= 0)
            return null;

        var formattedTooltip = new Tooltip();
        formattedTooltip.SetMessage(FormattedMessage.FromMarkupPermissive(tooltip.ToString().Trim()));
        return formattedTooltip;
    }
}
