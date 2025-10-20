using Content.Client.ContextMenu.UI;
using Content.Client.Gameplay;
using Content.Client.Viewport;
using Content.Shared.Administration;
using Robust.Client.Input;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Console;

namespace Content.Client._Floof.Commands;

[AnyCommand]
internal sealed class ExpandCommand : LocalizedCommands
{
    private const string Hovered = "$HOVERED";

    public override string Command => "expand";

    public override string Help => LocalizationManager.GetString($"cmd-{Command}-help", ("command", Command));

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        // Not sure which, but if we use these as dependencies we get a cycle,
        // so let's just resolve all of them at execution time.
        var inputManager = IoCManager.Resolve<IInputManager>();
        var uiManager = IoCManager.Resolve<IUserInterfaceManager>();
        var stateManager = IoCManager.Resolve<IStateManager>();
        var entityManager = IoCManager.Resolve<IEntityManager>();

        if (args.Length < 2)
        {
            shell.WriteError(Loc.GetString("cmd-invalid-arg-number-error"));
            return;
        }

        argStr = argStr.Remove(0, Command.Length + 1);

        if (argStr.Contains(Hovered))
        {
            if (stateManager.CurrentState is not GameplayStateBase screen)
                return;

            EntityUid? hoveredEntity = null;
            if (uiManager.CurrentlyHovered is IViewportControl vp
                && inputManager.MouseScreenPosition.IsValid)
            {
                var mousePosWorld = vp.PixelToMap(inputManager.MouseScreenPosition.Position);
                if (vp is ScalingViewport svp)
                    hoveredEntity = screen.GetClickedEntity(mousePosWorld, svp.Eye);
                else
                    hoveredEntity = screen.GetClickedEntity(mousePosWorld);
            }
            else if (uiManager.CurrentlyHovered is EntityMenuElement element)
                hoveredEntity = element.Entity;

            if (entityManager.GetNetEntity(hoveredEntity) is not {} netEntity)
            {
                shell.WriteLine(Loc.GetString($"cmd-{Command}-error-no-value", ("variable", Hovered)));
                return;
            }

            argStr = argStr.Replace(Hovered, netEntity.Id.ToString());
        }

        shell.ExecuteCommand(argStr);
    }
}
