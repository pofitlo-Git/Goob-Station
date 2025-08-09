namespace Content.Server._pofitlo.Hostage.Components;


[RegisterComponent]
public sealed partial class HostageComponent : Component
{
    [DataField]
    public EntityUid HostageTaker;
}
