using Content.Shared.Roles.Components;
using Robust.Shared.GameStates;

namespace Content.Shared.Necronomicon;

/// <summary>Mind role marker for the Kandarian Demon bound to the Necronomicon.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NecroDemonRoleComponent : BaseMindRoleComponent;

/// <summary>Mind role marker for a possessed crew member.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DeaditeRoleComponent : BaseMindRoleComponent;

/// <summary>Mind role marker for a summoned Demonspawn.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DemonspawnRoleComponent : BaseMindRoleComponent;

/// <summary>Client visual marker for the incorporeal demon (applies the chosen cosmetic form).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NecroDemonVisualsComponent : Component;
