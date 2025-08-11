using Content.Server.Atmos.Components;
using Content.Server.Body.Systems;
using Content.Server.Popups;
using Content.Shared._pofitlo.Hostage.Events;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Armor;
using Content.Shared.CombatMode;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using System.Linq;
using Content.Shared.Cuffs.Components;
using Content.Shared.Wieldable.Components;

namespace Content.Server._pofitlo.Hostage.System;

public sealed class HostageSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly DamageableSystem _damageSystem = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstreamSystem = default!;
    [Dependency] private readonly SharedGunSystem _shootSystem = default!;
    [Dependency] private readonly SharedMeleeWeaponSystem _meleeWeaponSystem = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CanBeTakenHostageComponent, GetVerbsEvent<AlternativeVerb>>(AddHostageVerb);
        SubscribeLocalEvent<CanBeTakenHostageComponent, HostageDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<CanBeTakenHostageComponent, DamageChangedEvent>(OnDamage);
    }

    private void OnDamage(EntityUid uid, CanBeTakenHostageComponent component, DamageChangedEvent args)
    {
        if (!component.IsHostage
            || args.Origin != component.HostageTakerUid
            || !IsWeaponInActiveHandValid(component.HostageTakerUid, component))
            return;

        AnyTypeWeaponMakeDamage(component, 100f);

    }

    private bool IsWeaponInActiveHandValid(EntityUid hostageTakerUid, CanBeTakenHostageComponent component)
    {
        return GetItemUidInActiveHand(hostageTakerUid) == component.HostageTakerWeaponUid;
    }
    private EntityUid? GetItemUidInActiveHand(EntityUid uid)
    {
        return TryComp<HandsComponent>(uid, out var handsComp)
            ? handsComp?.ActiveHand?.Container?.ContainedEntity
            : null;
    }
    private void AddHostageVerb(Entity<CanBeTakenHostageComponent> entity, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!IsVerbValid(args))
            return;

        var argsParameters = args;

        AlternativeVerb verb = new()
        {
            Act = () =>
            {
                TryStartDoAfter(entity, argsParameters);
            },
            Text = Loc.GetString("verb-take-hostage"),
            Priority = 2
        };
        args.Verbs.Add(verb);
    }

    private bool IsVerbValid(GetVerbsEvent<AlternativeVerb> args)
    {
        return args.CanInteract && args.CanAccess && args.Hands != null;
    }

    private void TryStartDoAfter(Entity<CanBeTakenHostageComponent> entity, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!IsCanHostage(args, entity))
            return;

        float modifier = MakeHostageModifierFromClothingOfVictim(args.Target);

        if (IsWeaponHeavy(args.Using))
            modifier *= 2;

        ProcessWeaponType(args.Using, entity.Comp);

        var doAfterArgs = new DoAfterArgs(EntityManager,
            args.User,
            entity.Comp.MakeHostageDoAfterDuration * modifier,
            new HostageDoAfterEvent(),
            entity.Owner,
            target: args.Target,
            used: entity.Owner)
        {
            DistanceThreshold = 0.5f,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnHandChange = true,
            BreakOnDropItem = true
        };

        var target = args.Target;

        _popupSystem.PopupEntity(Loc.GetString("take-hostage-start", ("target", target)), target, target, PopupType.LargeCaution);
        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private bool IsCanHostage(GetVerbsEvent<AlternativeVerb> args, Entity<CanBeTakenHostageComponent> entity)
    {
        return IsUserInCombatMode(entity, args.User)
            && IsUserAimingCorrectly(entity, args.User)
            && IsUserNotPacifist(entity);
    }

    private bool IsUserInCombatMode(Entity<CanBeTakenHostageComponent> entity, EntityUid HostageTakerUid)
    {
        if (!TryComp<CombatModeComponent>(HostageTakerUid, out var combatComp) || !combatComp.IsInCombatMode)
        {
            _popupSystem.PopupEntity(Loc.GetString("take-hostage-forbidden-not-combat-mode"), HostageTakerUid, HostageTakerUid, PopupType.LargeCaution);
            return false;
        }
        else return true;
    }

    private bool IsUserAimingCorrectly(Entity<CanBeTakenHostageComponent> entity, EntityUid HostageTakerUid)
    {
        if (!TryComp<TargetingComponent>(HostageTakerUid, out var targetingComp) || targetingComp.Target != entity.Comp.RequiredBodyPart)
        {
            _popupSystem.PopupEntity(Loc.GetString("take-hostage-forbidden-not-aiming-correctly"), HostageTakerUid, HostageTakerUid, PopupType.LargeCaution);
            return false;
        }
        else return true;
    }

    private bool IsUserNotPacifist(Entity<CanBeTakenHostageComponent> entity)
    {
        return (!HasComp<PacifiedComponent>(entity.Comp.HostageTakerUid));
    }

    private float MakeHostageModifierFromClothingOfVictim(EntityUid victimUid)
    {
        var headArmorUid = GetHeadArmorEntityUid(victimUid);

        float durationModifier = 1f + GetSumOfDefenseParametersOfHeadArmor(headArmorUid);

        if (HasComp<PressureProtectionComponent>(headArmorUid))
            durationModifier *= 2;

        if (IsVictimCuffed(victimUid))
            durationModifier *= 0.5f;

        return durationModifier;
    }

    private bool IsVictimCuffed(EntityUid victimUid)
    {
        return TryComp<CuffableComponent>(victimUid, out var cuffableComp)
            && cuffableComp.Container.Count != 0;
    }

    private EntityUid? GetHeadArmorEntityUid(EntityUid victimUid)
    {
        return TryComp<InventoryComponent>(victimUid, out var inventoryComp)
            ? inventoryComp.Containers.FirstOrDefault(c => c.ID == "head")?.ContainedEntity
            : null;
    }

    private float GetSumOfDefenseParametersOfHeadArmor(EntityUid? headArmorUid)
    {
        if (!TryComp<ArmorComponent>(headArmorUid, out var headArmorComp))
            return 0f;

        var bluntResist = headArmorComp.Modifiers.Coefficients.GetValueOrDefault("Blunt");
        var slashResist = headArmorComp.Modifiers.Coefficients.GetValueOrDefault("Slash");
        var piercingResist = headArmorComp.Modifiers.Coefficients.GetValueOrDefault("Piercing");

        return bluntResist + slashResist + piercingResist;
    }

    private bool IsWeaponHeavy(EntityUid? weaponUid)
    {
        return HasComp<WieldableComponent>(weaponUid);
    }
    private void ProcessWeaponType(EntityUid? weaponUid, CanBeTakenHostageComponent component)
    {
        if (weaponUid is not { } validWeaponUid)
            return;

        component.HostageTakerWeaponUid = validWeaponUid;

        if (TryProcessMeleeWeapon(validWeaponUid, component))
            return;

        ProcessGunWeapon(validWeaponUid, component);
    }

    private bool TryProcessMeleeWeapon(EntityUid weaponUid, CanBeTakenHostageComponent component)
    {
        if (!IsCuttingWeapon(weaponUid))
            return false;

        component.WeaponType = WeaponType.Melee;
        component.Range = 1.5f;
        return true;
    }

    private bool IsCuttingWeapon(EntityUid weaponUid)
    {
        return TryComp<MeleeWeaponComponent>(weaponUid, out var meleeComp)
            && meleeComp.Damage.DamageDict.ContainsKey("Slash");
    }
    private void ProcessGunWeapon(EntityUid weaponUid, CanBeTakenHostageComponent component)
    {
        if (!HasComp<GunComponent>(weaponUid))
            return;

        component.WeaponType = WeaponType.Ranged;
        component.Range = 3f;
    }
    private void OnDoAfter(EntityUid uid, CanBeTakenHostageComponent component, DoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target is not { } target)
            return;

        component.HostageTakerUid = args.User;
        component.HostageUid = target;
        component.IsHostage = true;
        component.WaitingToExecute = true;

        PopupOnHostageTakeSuccess(component);
    }

    private void PopupOnHostageTakeSuccess(CanBeTakenHostageComponent component)
    {
        var message = component.WeaponType switch
        {
            WeaponType.Melee => "take-hostage-success-with-melee",
            WeaponType.Ranged => "take-hostage-success-with-range",
            _ => ""
        };
        _popupSystem.PopupEntity(Loc.GetString(message, ("target", component.HostageUid), ("hostTaker", component.HostageTakerUid)), component.HostageTakerUid, PopupType.LargeCaution);
    }

    private DamageSpecifier CreateDamageSpecifier(string damageType, float damageAmount)
    {
        return new DamageSpecifier(_prototypes.Index<DamageTypePrototype>(damageType), damageAmount);
    }

    private void ApplyDamageToBodyPart(EntityUid targetUid, string damageType, float damageAmount, TargetBodyPart bodyPart)
    {
        var damageSpec = CreateDamageSpecifier(damageType, damageAmount);
        _damageSystem.TryChangeDamage(uid: targetUid,
                                      damage: damageSpec,
                                      canMiss: false,
                                      targetPart: bodyPart);
    }

    public override void Update(float frameTime)
    {
        var hostageQuery = EntityQueryEnumerator<CanBeTakenHostageComponent, TransformComponent>();
        while (hostageQuery.MoveNext(out var uid, out var hostageComp, out var transform))
        {
            if (hostageComp.NextUpdate > _timing.CurTime || !hostageComp.IsHostage)
                continue;

            hostageComp.NextUpdate = _timing.CurTime + hostageComp.UpdateInterval;

            var hostageTakerCoord = Transform(hostageComp.HostageTakerUid).Coordinates;

            if (!_transform.InRange(hostageTakerCoord, transform.Coordinates, hostageComp.Range))
            {
                if(hostageComp.WeaponType == WeaponType.Melee && TryMakeMeleeLightAttack(hostageComp))
                    AnyTypeWeaponMakeDamage(hostageComp, 100f);
                if (hostageComp.WeaponType == WeaponType.Ranged)
                    TryMakeShoot(hostageComp, transform.Coordinates);

                StopTakeHostage(hostageComp);
            }
        }
    }

    private void StopTakeHostage(CanBeTakenHostageComponent component)
    {
        _popupSystem.PopupEntity(Loc.GetString("take-hostage-stop"), component.HostageTakerUid, component.HostageTakerUid, PopupType.LargeCaution);
        component.IsHostage = false;
    }

    private void AnyTypeWeaponMakeDamage(CanBeTakenHostageComponent component, float damageValue)
    {
        var result = component.WeaponType switch
        {
            WeaponType.Melee => new { DamageType = "Slash", Massage = "take-hostage-stab"},
            WeaponType.Ranged => new { DamageType = "Piercing", Massage = "take-hostage-shoot"},
            _ => new { DamageType = "", Massage = "" }
        };

        if (result.DamageType != null)
        {
            MakeDamageInHeadWithBloodLoss(component, damageValue, result.DamageType, result.Massage);
            StopTakeHostage(component);
        }
    }

    private void MakeDamageInHeadWithBloodLoss(CanBeTakenHostageComponent component, float amountOfDamage, string damageType, string massage)
    {
        _popupSystem.PopupEntity(Loc.GetString(massage, ("target", component.HostageUid), ("hostTaker", component.HostageTakerUid)), component.HostageTakerUid, PopupType.LargeCaution);
        ApplyDamageToBodyPart(component.HostageUid, damageType, amountOfDamage, TargetBodyPart.Head);
        _bloodstreamSystem.SpillAllSolutions(component.HostageUid);
    }
    private bool TryMakeShoot(CanBeTakenHostageComponent component, EntityCoordinates toCoordinates)
    {
        if (!TryComp<GunComponent>(component.HostageTakerWeaponUid, out var gunComp)
            || IsWeaponInActiveHandValid(component.HostageTakerWeaponUid, component))
            return false;

        _shootSystem.AttemptShoot(component.HostageTakerUid, component.HostageTakerWeaponUid, gunComp, toCoordinates); //TODO проверить - можно ли стрелять без патрон
        return true;
    }

    private bool TryMakeMeleeLightAttack(CanBeTakenHostageComponent component)
    {
        return TryComp<MeleeWeaponComponent>(component.HostageTakerWeaponUid, out var weaponComp)
            && _meleeWeaponSystem.AttemptLightAttack(component.HostageTakerUid, component.HostageTakerWeaponUid, weaponComp, component.HostageUid);
    }
}
