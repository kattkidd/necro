using Content.Shared.Necronomicon;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client.Necronomicon;

/// <summary>
/// Shows the demon's chosen form (cosmetic only) on its incorporeal sprite, tinted with its aura colour and faded
/// so it still reads as a spirit.
/// </summary>
public sealed partial class NecroDemonVisualizerSystem : VisualizerSystem<NecroDemonVisualsComponent>
{
    private static readonly Color DefaultTint = Color.FromHex("#ff8080");

    protected override void OnAppearanceChange(EntityUid uid, NecroDemonVisualsComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (AppearanceSystem.TryGetData<string>(uid, NecroDemonVisuals.Form, out var form, args.Component))
        {
            var split = form.Split('|');
            if (split.Length == 2 && !string.IsNullOrEmpty(split[0]) && !string.IsNullOrEmpty(split[1]))
                SpriteSystem.LayerSetSprite((uid, args.Sprite), 0, new SpriteSpecifier.Rsi(new ResPath(split[0]), split[1]));
        }

        var tint = AppearanceSystem.TryGetData<Color>(uid, NecroDemonVisuals.Tint, out var aura, args.Component)
            ? Color.InterpolateBetween(Color.White, aura, 0.5f)
            : DefaultTint;

        SpriteSystem.SetColor((uid, args.Sprite), tint.WithAlpha(0.65f));
    }
}
