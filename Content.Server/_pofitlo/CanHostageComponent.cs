using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Content.Shared._Shitmed.Targeting;

namespace Content.Server._pofitlo;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class CanHostageComponent : Component
{
    [DataField]
    public float MakeHostageDoAfterDuration = 3f;

    [DataField, AutoPausedField]
    public TimeSpan NextUpdate = TimeSpan.Zero;

    [DataField, AutoPausedField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.5); //TODO быть может, сделать чаще

    public EntityUid HostageTakerUid;

    public EntityUid HostageUid;

    [DataField]
    public float Range = 3f; //TODO подобрать и переименовать

    [DataField]
    public bool IsHostage = false;

    [DataField]
    public TargetBodyPart RequiredBodyPart = TargetBodyPart.Head;

    [DataField]
    public WeaponType WeaponType;
}

public enum WeaponType
{
    Melee,
    Ranged
}

