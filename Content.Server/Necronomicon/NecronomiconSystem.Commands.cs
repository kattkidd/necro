using Content.Server.Administration;
using Content.Server.Necronomicon.Components;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.Necronomicon;

/// <summary>
/// Admin/testing command: <c>necrosp &lt;amount&gt;</c> gives Soul Power to the demon you are playing
/// (or whose physical form you are wearing). Fear only comes from real players, so this makes solo testing possible.
/// </summary>
public sealed partial class NecronomiconSystem
{
    [Dependency] private IConsoleHost _consoleHost = default!;

    private void InitializeCommands()
    {
        _consoleHost.RegisterCommand("necrosp",
            "Give Soul Power to the Kandarian Demon you are controlling.",
            "necrosp <amount>",
            NecroSpCommand);
    }

    [AdminCommand(AdminFlags.Fun)]
    private void NecroSpCommand(IConsoleShell shell, string argstr, string[] args)
    {
        if (args.Length != 1 || !float.TryParse(args[0], out var amount))
        {
            shell.WriteError("Usage: necrosp <amount>");
            return;
        }

        if (shell.Player?.AttachedEntity is not { } attached)
        {
            shell.WriteError("You aren't controlling anything.");
            return;
        }

        var demonUid = attached;
        if (TryComp<NecroManifestationComponent>(attached, out var form) && form.Demon is { } formDemon)
            demonUid = formDemon;

        if (!TryComp<NecroDemonComponent>(demonUid, out var demon))
        {
            shell.WriteError("You aren't a Kandarian Demon.");
            return;
        }

        AddSoulPower((demonUid, demon), amount, earned: false);
        shell.WriteLine($"Soul Power is now {GetSoulPower((demonUid, demon))}.");
    }
}
