using Content.Shared.Necronomicon;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client.Necronomicon;

/// <summary>
/// Shows the demon's chosen form (cosmetic only) on its incorporeal sprite, faded so it still reads as a spirit.
/// </summary>
public sealed partial class NecroDemonVisualizerSystem : VisualizerSystem<NecroDemonVisualsComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, NecroDemonVisualsComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!AppearanceSystem.TryGetData<string>(uid, NecroDemonVisuals.Form, out var form, args.Component))
            return;

        var split = form.Split('|');
        if (split.Length != 2 || string.IsNullOrEmpty(split[0]) || string.IsNullOrEmpty(split[1]))
            return;

        SpriteSystem.LayerSetSprite((uid, args.Sprite), 0, new SpriteSpecifier.Rsi(new ResPath(split[0]), split[1]));
        SpriteSystem.SetColor((uid, args.Sprite), Color.White.WithAlpha(0.6f));
    }
}
