// danielblnc's runtime builds: the ones this project has seen, the ones OptiScaler runs in place of the
// one it ships with, and the ones a person supplies themselves (UserRuntime) -- his supporter builds,
// which are never distributed. His own setup loads the runtime as version.dll; every route takes that
// file to the backup rather than run a second driver on one runtime, and uninstall puts it back.

namespace AmdNr.Core;

public static partial class Work
{
    /// <summary>The runtime builds this project has seen, by the start of their SHA-256: danielblnc's
    /// 0.2.14, 0.2.17, 0.3.0, 0.3.1, 0.3.3, 0.4.0, 0.4.1 and 0.4.2, the 0.4.3 and 0.5.0 he gives his supporters, and
    /// the 0.3.0, 0.4.0 and 0.4.1 the add-on pins. Any of them sitting in the game folder as version.dll is the
    /// author's own way of loading the runtime.</summary>
    private static readonly string[] KnownRuntimePrefixes =
    [
        "e145ff963b1ef614", "ddd82d313aa74c2e", "bc97f3b06718e190",
        "8321cae728d28cb7", "70af3fb757f83f71", "b108d6407eb7f094",
        "907b30a61644a6d7", "d62be3d8b9fbb3c6", "ff6feffa41abccce",
        "823063eb4c76b133", "c8808716c286a34f", "8aa2dcc5b6596aca", "d1e320862a8763ac",
        "cddfb09e01934795",
    ];

    /// <summary>The name the author's setup loads the runtime under.</summary>
    internal const string AuthorRuntimeName = "version.dll";

    /// <summary>The weights file beside the author's version.dll is the author's runtime's: it builds
    /// one there from the game's nvngx_dlssnr.dll, so it says nothing about an install of ours.</summary>
    internal static bool IsAuthorsWeights(string dir, string name) =>
        name == WeightsName && File.Exists(Path.Combine(dir, AuthorRuntimeName));

    /// <summary>The danielblnc runtimes OptiScaler runs in place of the one it ships with: SHA-256, runtime
    /// version, and the first OptiScaler release whose AmdLayout.h accepts that build (0.2.17 is in there too,
    /// but the release's own Setup refuses it).</summary>
    private static readonly (string Sha, Version Runtime, Version Since)[] AcceptedRuntimes =
    [
        ("8321cae728d28cb7632d0d58d3d913e91132bf7645c126505698fbe4cd5a0138", new(0, 3, 0), new(0, 1, 0)),
        ("b108d6407eb7f094a4f9111edd778eee7b978b648d413a9fc7aeedfdd914c154", new(0, 3, 1), new(0, 1, 0)),
        ("d62be3d8b9fbb3c6c81982c4ddb3dfa00eb9662e3206925cbe5b7e1bc6798b80", new(0, 4, 0), new(0, 4, 1)),
        ("823063eb4c76b1334fd1800c41798873ae61d4016af0406f1f0b9dce57b1d376", new(0, 4, 1), new(0, 4, 2)),
        ("8aa2dcc5b6596aca97995dbfd4e0a9790d8c15108495e0ed154dd15dbb5b465a", new(0, 4, 2), new(0, 4, 3)),
        ("d1e320862a8763ac39e7ce194536d4b6c55ba61bae9e8a92753cec32df67a457", new(0, 4, 3), new(0, 4, 3)),
        ("cddfb09e019347957bf7b96c95c0e900e8d3062dfaed697a8a96b0a039aec31a", new(0, 5, 0), new(0, 4, 4)),
    ];

    /// <summary>"0.4.3-amd-nr" or "0.4.2" as a version, or null.</summary>
    private static Version? VersionOf(string text) =>
        Version.TryParse(text.Split('-')[0], out var v) ? v : null;

    /// <summary>The runtime version of a build the given OptiScaler release runs, or null.</summary>
    internal static Version? AcceptedRuntime(string sha, string optiScalerVersion) =>
        VersionOf(optiScalerVersion) is { } opti
            ? AcceptedRuntimes.FirstOrDefault(r => r.Sha == sha && opti >= r.Since).Runtime
            : null;

    /// <summary>The runtime an install takes from the game folder in place of the payload's: the author's
    /// version.dll when the chosen OptiScaler runs that build, or else the pass 1 an earlier install put in,
    /// when it is a build that OptiScaler runs and newer than the payload's (a build of the author's kept
    /// across updates). Null when the payload's is the one.</summary>
    private static (byte[] Bytes, Version Runtime, string From)? OwnRuntime(string dir, PayloadPins pins)
    {
        foreach (var name in new[] { AuthorRuntimeName, OptiPasses[0] })
        {
            var path = Path.Combine(dir, name);
            if (Engine.SizeOf(path) is not (> 7_000_000 and < 40_000_000)) continue;
            var bytes = Engine.Read(path);
            if (AcceptedRuntime(Engine.Sha(bytes), pins.OptiScalerVersion) is not { } runtime) continue;
            if (name == AuthorRuntimeName || VersionOf(pins.OptiRuntimeVersion) is { } shipped && runtime > shipped)
                return (bytes, runtime, name);
        }
        return null;
    }

    /// <summary>Whether a runtime pass an install recorded is current although the newest payload pins
    /// another build: one that release runs and newer than the one it ships (<see cref="OwnRuntime"/>).</summary>
    internal static bool OwnRuntimeIsCurrent(PayloadManifest payload, string name, string sha)
    {
        if (!OptiPasses.Contains(name)) return false;
        payload = payload.Newest(PayloadManifest.OptiScalerComponent);
        return payload.Has(PayloadManifest.OptiScalerComponent) && payload.Has(PayloadManifest.OptiRuntimeComponent)
               && AcceptedRuntime(sha, payload.Component(PayloadManifest.OptiScalerComponent).Version) is { } own
               && VersionOf(payload.Component(PayloadManifest.OptiRuntimeComponent).Version) is { } shipped
               && own > shipped;
    }

    private static void CheckRuntimeAsVersionDll(string dir, PayloadPins pins, Report report)
    {
        var path = Path.Combine(dir, AuthorRuntimeName);
        // Size first, so this stays a stat() for every version.dll that is something else.
        if (Engine.SizeOf(path) is not (> 7_000_000 and < 40_000_000)) return;
        var sha = Engine.HashFile(path);
        if (AcceptedRuntime(sha, pins.OptiScalerVersion) is { } runtime)
        {
            report.Info(
                $"version.dll here is danielblnc's runtime {runtime}, loaded the way its author's setup loads it. "
                + "OptiScaler takes it as its runtime (dlssnr_amd_pass1-3.dll) in place of the download, and "
                + "version.dll goes to the backup, since both at once would be two drivers on one runtime. "
                + "Uninstall puts it back.");
            return;
        }
        if (!KnownRuntimePrefixes.Any(p => sha.StartsWith(p, StringComparison.Ordinal))) return;
        report.Err(
            "version.dll here is the DLSS-NR-on-AMD runtime itself, loaded the way its author's setup "
            + $"loads it, in a build OptiScaler {pins.OptiScalerVersion} does not run. Beside OptiScaler it "
            + "would be two drivers on one runtime. Move version.dll out of this folder and run this again.");
    }

    // -- Builds a person supplies ------------------------------------------------------------------

    /// <summary>The builds a person can supply that an install of this route, at the version these pins
    /// carry, runs: patched, by an add-on (or bridge) from the build's addon_since on, or as it is, by an
    /// OptiScaler whose <see cref="AcceptedRuntimes"/> has it.</summary>
    public static IReadOnlyList<UserRuntime> OfferedRuntimes(PayloadPins pins, Preset preset) =>
        pins.UserRuntimes.Where(b => Runs(pins, preset, b)).ToList();

    private static bool Runs(PayloadPins pins, Preset preset, UserRuntime build) =>
        preset.IsOptiScaler()
            ? AcceptedRuntime(build.OriginalSha256, pins.OptiScalerVersion) is not null
            : build.RunsOn(AddonVersionFor(pins, preset));

    private static string AddonVersionFor(PayloadPins pins, Preset preset) =>
        preset.Route() == Route.X86 ? pins.BridgeVersion : pins.AddonVersion;

    /// <summary>What an install puts in place of the download's runtime when a person supplied a build: read
    /// from <paramref name="source"/> (see <see cref="UserRuntime.Read"/>), and patched for the add-on on the
    /// ReShade routes. Null is the download's: nothing supplied, or a version that does not run that build,
    /// which is said. A file that is not a listed build, or does not patch to its listed hash, is an error,
    /// and nothing is written.</summary>
    private static (UserRuntime Build, byte[] Bytes)? Supplied(string? source, PayloadPins pins, Preset preset, Report report)
    {
        if (string.IsNullOrWhiteSpace(source)) return null;
        try
        {
            var (build, original) = UserRuntime.Read(source, pins.UserRuntimes);
            if (Runs(pins, preset, build)) return (build, preset.IsOptiScaler() ? original : build.Patched(original));
            report.Warn(
                preset.IsOptiScaler()
                    ? $"OptiScaler {pins.OptiScalerVersion} does not run danielblnc's runtime {build.Name}, the build you supplied, so it is left out."
                    : !build.Patchable
                        ? $"The payload list has no patch yet for danielblnc's runtime {build.Name}, the build you supplied, "
                          + "so the add-on cannot drive it: the runtime is the download's."
                        : $"Add-on v{AddonVersionFor(pins, preset)} does not run danielblnc's runtime {build.Name}, the build you "
                          + $"supplied (v{build.AddonSince} and later do), so the runtime is the download's.");
        }
        catch (InstallException e)
        {
            report.Err(e.Message);
        }
        return null;
    }

    /// <summary>What the pre-flight says about a build a person supplied.</summary>
    private static void CheckSupplied(string? source, PayloadPins pins, Preset preset, Report report)
    {
        if (Supplied(source, pins, preset, report) is { } own)
            report.Ok($"danielblnc's runtime {own.Build.Name}, from your own file, is checked"
                      + (preset.IsOptiScaler() ? "" : " and patched for the add-on") + ": it goes in place of the download's.");
    }

    /// <summary>The build a person supplied that this game folder runs: the one an install of this app put
    /// in (patched beside the add-on, as it is beside OptiScaler), or else danielblnc's own version.dll. What
    /// the sheet goes by until somebody picks, the way <see cref="HasMochizuki"/> is; null is the download.</summary>
    public static UserRuntime? UserRuntimeIn(string gameDir, IReadOnlyList<UserRuntime> builds)
    {
        var dir = ResolveSource(gameDir);
        if (builds.Count == 0 || dir.Length == 0 || !Directory.Exists(dir)) return null;
        foreach (var route in new[] { Route.X64, Route.X86 })
            if (InstalledManifest(dir, route)?.Entries.FirstOrDefault(e => e.Name == RuntimeName) is { } entry
                && File.Exists(Path.Combine(dir, RuntimeName))
                && builds.FirstOrDefault(b => entry.Hash == b.OriginalSha256 || entry.Hash == b.PatchedSha256) is { } build)
                return build;
        var loader = Path.Combine(dir, AuthorRuntimeName);
        return builds.FirstOrDefault(b => PayloadCache.Verified(loader, b.OriginalSize, b.OriginalSha256));
    }

    /// <summary>Where this machine has a build for an install to read: the copy this app kept, or else
    /// danielblnc's version.dll or the runtime an install put in the game folder, kept on the way so the next
    /// game does not ask. Null when the person has to supply it.</summary>
    public static string? FindUserRuntime(UserRuntime build, string gameDir)
    {
        if (build.Kept() is { } kept) return kept;
        var dir = ResolveSource(gameDir);
        if (dir.Length == 0) return null;
        foreach (var name in new[] { AuthorRuntimeName, RuntimeName })
        {
            var path = Path.Combine(dir, name);
            if (Engine.SizeOf(path) != build.OriginalSize) continue;
            try
            {
                UserRuntime.Keep(path, [build]);
                return build.Kept();
            }
            catch (InstallException)
            {
                // Somebody else's file under that name, or another build: not a source.
            }
        }
        return null;
    }

    /// <summary>Whether the runtime a ReShade install recorded is current although the payload pins another:
    /// a build a person supplied, patched, that the add-on (or bridge) the payload pins runs.</summary>
    internal static bool UserRuntimeIsCurrent(PayloadManifest payload, Route route, string name, string sha)
    {
        var own = route == Route.X86 ? PayloadManifest.BridgeComponent : PayloadManifest.AddonComponent;
        return name == RuntimeName && payload.Has(own)
               && (payload.UserRuntimes ?? []).Any(b => b.PatchedSha256 == sha && b.RunsOn(payload.Component(own).Version));
    }

    /// <summary>Whether version.dll here is danielblnc's own loader: his runtime the way his setup installs
    /// it, in a build this project has seen or one a person can supply. Beside the add-on that is two drivers
    /// on one runtime -- why OptiScaler takes it too -- so the ReShade routes move it to the backup, and his
    /// weights beside it stay his (<see cref="IsAuthorsWeights"/>).</summary>
    private static bool AuthorsLoaderHere(string dir, PayloadPins pins)
    {
        var path = Path.Combine(dir, AuthorRuntimeName);
        if (Engine.SizeOf(path) is not { } size
            || !(size is > 7_000_000 and < 40_000_000 || pins.UserRuntimes.Any(b => b.OriginalSize == size)))
            return false;
        var sha = Engine.HashFile(path);
        return KnownRuntimePrefixes.Any(p => sha.StartsWith(p, StringComparison.Ordinal))
               || pins.UserRuntimes.Any(b => b.OriginalSha256 == sha);
    }

    private static string SuppliedInstalled(UserRuntime build) =>
        $"The runtime is danielblnc's {build.Name} from your own file, patched for the add-on, in place of the download's.";

    private const string AuthorsLoaderMoves =
        "version.dll here is danielblnc's own loader for his runtime, the way his setup installs it. Beside the "
        + "add-on it would be two drivers on one runtime, so an install moves it to the backup, and uninstall puts it back.";
}
