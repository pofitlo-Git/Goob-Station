using Content.Server.Body.Systems;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Popups;
using Content.Server.Tools.Innate;
using Content.Shared.UserInterface;
using Content.Shared.Body.Components;
using Content.Shared._Imp.Drone;
using Content.Shared.Emoting;
using Content.Shared.Examine;
using Content.Shared.Ghost;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Content.Shared.Whitelist;
using Robust.Shared.Timing;
using Content.Shared._pofitlo.Hostage.Events;
using Content.Server._pofitlo.Hostage.Components;



namespace Content.Server._pofitlo.Hostage.System;

public sealed class HostageSystem : EntitySystem
{
    public override void Initialize()
    {
        //SubscribeLocalEvent<HostageComponent, HostageDoAfterEvent>(OnHostageDoAfter);
    }
}
