using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._pofitlo.Hostage.Events;

[Serializable, NetSerializable]
public sealed partial class HostageDoAfterEvent : SimpleDoAfterEvent
{
}

