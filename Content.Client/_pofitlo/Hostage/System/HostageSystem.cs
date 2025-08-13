using Content.Shared._pofitlo.Hostage.System;
using Content.Shared._pofitlo.Hostage.Events;
using Content.Shared.Weapons.Ranged.Events;
using Content.Client.Weapons.Ranged.Systems;
using System.Numerics;
using Content.Client.IoC;
using Content.Client.Items;
using Content.Client.Resources;
using Content.Client.Stylesheets;
using Content.Client.Weapons.Ranged.Components;
using Content.Client.Weapons.Ranged.ItemStatus;
using Robust.Client.Animations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;


namespace Content.Client._pofitlo.Hostage.System;

public sealed class HostageSystem : SharedHostageSystem
{
    [Dependency] private readonly GunSystem _gunSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<HostageAttemptShootEvent>(OnHostageAttemptShoot);
    }

    private void OnHostageAttemptShoot(HostageAttemptShootEvent args)
    {
        var shooter = GetEntity(args.Shooter);
        var weapon = GetEntity(args.Weapon);

        if (!TryComp<AmmoCounterComponent>(weapon, out var ammoCounterComp))
            return;

        _gunSystem.PublicUpdateAmmoCount(weapon, ammoCounterComp);
    }
}

