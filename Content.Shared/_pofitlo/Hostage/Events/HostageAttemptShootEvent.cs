using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._pofitlo.Hostage.Events;

[Serializable, NetSerializable]
public sealed partial class HostageAttemptShootEvent : EntityEventArgs
{
    public NetEntity Shooter { get; }
    public NetEntity Weapon { get; }

    public HostageAttemptShootEvent(NetEntity shooter, NetEntity weapon)
    {
        Shooter = shooter;
        Weapon = weapon;
    }
}

