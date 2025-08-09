using Content.Server.Popups;
using Content.Shared._pofitlo.Hostage.Events;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Timing;
using Robust.Shared.Prototypes;
using Content.Shared.Damage.Prototypes;
using Content.Server.Body.Systems;
using Content.Shared.Damage;


namespace Content.Server._pofitlo.Hostage.System;

public sealed class MakeHostageSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly DamageableSystem _damageSystem = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly BloodstreamSystem _bloodstreamSystem = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CanHostageComponent, GetVerbsEvent<AlternativeVerb>>(AddHostageVerb);
        SubscribeLocalEvent<CanHostageComponent, HostageDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<CanHostageComponent, DamageChangedEvent>(OnDamage);
    }

    private void OnDamage(EntityUid uid, CanHostageComponent component, DamageChangedEvent args)
    {
        if (!component.IsHostage)
            return;
        if (args.Origin != component.HostageTakerUid)
            return;

        if(component.WeaponType == WeaponType.Melee)
        {
            ApplyDamageToBodyPart(component.HostageUid, "Slash", 150f, TargetBodyPart.Head); //TODO вынести в компонент, забалансить
            _bloodstreamSystem.TryModifyBleedAmount(component.HostageUid, 60f); //TODO затестить. Быть может, поставить компонент на выплескивание крови
        }
        if(component.WeaponType == WeaponType.Ranged) // TODO сделать проверку на то, что урон нанесен именно пулей
        {
            ApplyDamageToBodyPart(component.HostageUid, "Piercing", 150f, TargetBodyPart.Head); //TODO вынести в компонент, забалансить
        }

    }
    private void AddHostageVerb(Entity<CanHostageComponent> entity, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!IsCanHostage(args, entity))
            return;

        ProcessWeaponType(args.Using, entity.Comp);
        // Создаем DoAfter аргументы
        var doAfterArgs = new DoAfterArgs(EntityManager,
            args.User,
            entity.Comp.MakeHostageDoAfterDuration,
            new HostageDoAfterEvent(),
            entity.Owner,
            target: args.Target,
            used: entity.Owner)
        {
            DistanceThreshold = 2f,
            BreakOnDamage = true
        };

        // Создаем AltVerb
        AlternativeVerb verb = new()
        {
            Act = () =>
            {
                _popupSystem.PopupEntity(Loc.GetString("Test1", ("target", entity.Owner)), entity.Owner, PopupType.LargeCaution);
                _doAfter.TryStartDoAfter(doAfterArgs);
            },
            Text = Loc.GetString("hostage-system-verb-take-hostage"),
            Priority = 2
        };
        args.Verbs.Add(verb);
    }

    private void ProcessWeaponType(EntityUid? weaponUid, CanHostageComponent component)
    {
        if (weaponUid is not { } validWeaponUid)
            return;

        var meleeWeaponComponent = GetWeaponMeleeTypeComponent(validWeaponUid); // TODO переместить нормально по функциям. processed - зло
        if (meleeWeaponComponent != null && meleeWeaponComponent.Damage.DamageDict.ContainsKey("Slash")) // TODO вынести в компач
        {
            component.WeaponType = WeaponType.Melee;
        }
        var gunComponent = GetIsWeaponGunTypeComponent(validWeaponUid);
        if(gunComponent != null)
        {
            component.WeaponType = WeaponType.Ranged;
        }

    }

    private MeleeWeaponComponent? GetWeaponMeleeTypeComponent(EntityUid weaponUid)
    {
        if (TryComp<MeleeWeaponComponent>(weaponUid, out var weaponComp))
            return weaponComp;
        else return null;
    }
    private GunComponent? GetIsWeaponGunTypeComponent(EntityUid weaponUid)
    {
        if (TryComp<GunComponent>(weaponUid, out var weaponComp))
            return weaponComp;
        else return null;
    }
    private bool IsCanHostage(GetVerbsEvent<AlternativeVerb> args, Entity<CanHostageComponent> entity)
    {
        return IsVerbValid(args)
            && IsTargetExist(args)
            && IsUserInCombatMode(args, entity)
            && IsUserAimingCorrectly(args, entity);
    }

    private bool IsUserInCombatMode(GetVerbsEvent<AlternativeVerb> args, Entity<CanHostageComponent> entity)
    {
        if (TryComp<CombatModeComponent>(args.User, out var combatComp) && !combatComp.IsInCombatMode)
        {
            _popupSystem.PopupEntity(Loc.GetString("Test1", ("target", entity.Owner)), entity.Owner, PopupType.LargeCaution);
            return false;
        }
        else return true;
    }

    private bool IsUserAimingCorrectly(GetVerbsEvent<AlternativeVerb> args, Entity<CanHostageComponent> entity)
    {
        if (TryComp<TargetingComponent>(args.User, out var targetingComp) && targetingComp.Target != entity.Comp.RequiredBodyPart)
        {
            _popupSystem.PopupEntity(Loc.GetString("Test2", ("target", entity.Owner)), entity.Owner, PopupType.LargeCaution);
            return false;
        }
        else return true;
    }

    private bool IsTargetExist(GetVerbsEvent<AlternativeVerb> args)
    {
        return args.Target != null;
    }

    private bool IsVerbValid(GetVerbsEvent<AlternativeVerb> args)
    {
        return args.CanInteract && args.CanAccess && args.Hands != null;
    }
    private void OnDoAfter(EntityUid uid, CanHostageComponent component, DoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target is not { } target)
            return;

        _popupSystem.PopupEntity(Loc.GetString("TT2", ("target", uid)), uid, PopupType.LargeCaution);
        component.HostageTakerUid = args.User;
        component.HostageUid = target;
        component.IsHostage = true;

        // Наносим небольшой урон при захвате заложника
        if (component.WeaponType == WeaponType.Melee)
        {
            //ApplyDamageToBodyPart(target, "Blunt", 2.0f, BodyPartType.Head, uid);
        }
        else if (component.WeaponType == WeaponType.Ranged)
        {
            //ApplyDamageToBodyPart(target, "Piercing", 3.0f, BodyPartType.Torso, uid);
        }
    }

    /// <summary>
    /// Создает DamageSpecifier для нанесения урона
    /// </summary>
    /// <param name="damageType">Тип урона (например, "Slash", "Blunt", "Piercing")</param>
    /// <param name="damageAmount">Количество урона</param>
    /// <returns>DamageSpecifier для нанесения урона</returns>
    private DamageSpecifier CreateDamageSpecifier(string damageType, float damageAmount)
    {
        return new DamageSpecifier(_prototypes.Index<DamageTypePrototype>(damageType), damageAmount);
    }

    /// <summary>
    /// Наносит урон по определенной части тела
    /// </summary>
    /// <param name="targetUid">UID цели</param>
    /// <param name="damageType">Тип урона</param>
    /// <param name="damageAmount">Количество урона</param>
    /// <param name="bodyPart">Часть тела для нанесения урона</param>
    /// <param name="sourceUid">Источник урона (опционально)</param>
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
        var hostageQuery = EntityQueryEnumerator<CanHostageComponent, TransformComponent>();
        while (hostageQuery.MoveNext(out var uid, out var hostageComp, out var transform))
        {
            if (hostageComp.NextUpdate > _timing.CurTime)
                continue;
            if (!hostageComp.IsHostage)
                continue;

            hostageComp.NextUpdate = _timing.CurTime + hostageComp.UpdateInterval;

            var hostageCoord = Transform(hostageComp.HostageTakerUid).Coordinates;

            if (!_transform.InRange(hostageCoord, transform.Coordinates, hostageComp.Range)) //TODO transform.Coordinates - глупенькость. Возможно
            {
                if(hostageComp.WeaponType == WeaponType.Melee) // TODO все в отдельные компоненты
                {
                    // Наносим урон по голове за попытку сбежать
                    ApplyDamageToBodyPart(hostageComp.HostageUid, "Slash", 150f, TargetBodyPart.Head); //TODO вынести в компонент, забалансить
                    _bloodstreamSystem.TryModifyBleedAmount(hostageComp.HostageUid, 40f); //TODO затестить
                }
                else if(hostageComp.WeaponType == WeaponType.Ranged)
                {
                    // Для огнестрельного оружия наносим урон по туловищу
                    ApplyDamageToBodyPart(hostageComp.HostageUid, "Slash", 10.0f, TargetBodyPart.Head);
                }
                hostageComp.IsHostage = false;
            }
        }
    }
}
