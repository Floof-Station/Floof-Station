using Content.Server.Administration;
using Content.Server.Body.Systems;
using Content.Shared.Administration;
using Content.Shared.Body.Components;
using Content.Shared.Body.Prototypes;
using Content.Shared.Body.Systems;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed;


namespace Content.Server._Floof.Body.Commands;

[ToolshedCommand, AdminCommand(AdminFlags.Admin)]
sealed class BodyCommand : ToolshedCommand
{
    public static readonly ProtoId<BodyPrototype> DefaultBodyPrototype = "Adminbus";

    [CommandImplementation("default")]
    public EntityUid AddDefault(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid input)
    {
        return AddPrototype(ctx, input, DefaultBodyPrototype);
    }

    [CommandImplementation("prototype")]
    public EntityUid AddPrototype(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid input,
        [CommandArgument] ProtoId<BodyPrototype> proto)
    {
        if (HasComp<BodyComponent>(input))
        {
            ctx.WriteLine($"Warning: entity {input} already has a body. Refusing to add another.");
            return input;
        }

        EnsureComp<HandsComponent>(input);
        EnsureComp<ComplexInteractionComponent>(input); // required to pick up items
        EntityManager.AddComponent(input, new BodyComponent
        {
            Prototype = proto
        });

        return input;
    }

    [CommandImplementation("set_required_legs")]
    public EntityUid SetRequiredLegs(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid input,
        [CommandArgument] int required)
    {
        if (TryComp<BodyComponent>(input, out var body))
        {
            body.RequiredLegs = required;
        }
        return input;
    }

    [CommandImplementation("remove")]
    public EntityUid RemoveBody(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid input)
    {
        if (TryComp<BodyComponent>(input, out var body))
        {
            // Yes we have to do this because otherwise the system won't delete them
            foreach (var part in EntityManager.System<BodySystem>().GetBodyChildren(input, body))
                EntityManager.QueueDeleteEntity(part.Id);

            RemComp<BodyComponent>(input);
        }
        return input;
    }
}
