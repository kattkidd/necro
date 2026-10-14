using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Necronomicon;

/// <summary>
/// What the demon's player needs on screen: Soul Power, possession charges and where the book is.
/// Kept up to date by the server and read by the HUD alerts on the client.
/// Present on the incorporeal demon and on its physical form while it is worn.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class NecroDemonStatusComponent : Component
{
    [DataField, AutoNetworkedField]
    public int SoulPower;

    [DataField, AutoNetworkedField]
    public int Charges;

    [DataField, AutoNetworkedField]
    public NecroBookState BookState = NecroBookState.Ether;
}

[Serializable, NetSerializable]
public enum NecroBookState : byte
{
    /// <summary>Safe in the ether.</summary>
    Ether,

    /// <summary>In the physical world, unattended or carried by a Deadite.</summary>
    Manifested,

    /// <summary>Carried by a living, unpossessed person: it cannot leave.</summary>
    Held,

    /// <summary>On fire.</summary>
    Burning,
}
