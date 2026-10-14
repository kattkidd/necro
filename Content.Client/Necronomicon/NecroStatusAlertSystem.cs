using Content.Shared.Alert.Components;
using Content.Shared.Necronomicon;

namespace Content.Client.Necronomicon;

/// <summary>Feeds the demon's Soul Power and possession charge counters to the HUD alerts.</summary>
public sealed partial class NecroStatusAlertSystem : EntitySystem
{
    public const string SoulPowerAlert = "NecroSoulPower";
    public const string ChargesAlert = "NecroCharges";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NecroDemonStatusComponent, GetGenericAlertCounterAmountEvent>(OnGetCounterAmount);
    }

    private void OnGetCounterAmount(Entity<NecroDemonStatusComponent> ent, ref GetGenericAlertCounterAmountEvent args)
    {
        if (args.Handled)
            return;

        if (args.Alert.ID == SoulPowerAlert)
            args.Amount = Math.Clamp(ent.Comp.SoulPower, 0, 999);
        else if (args.Alert.ID == ChargesAlert)
            args.Amount = Math.Clamp(ent.Comp.Charges, 0, 999);
    }
}
