using System.Text;
using AmdNr.Core;

namespace AmdNr.Core.Tests;

/// <summary>danielblnc's standalone runtime left in a game folder by his setup, found by what it hashes to
/// rather than by its name. NBA 2K27 is the case: a chain loader as version.dll loading his runtime renamed
/// dlssnr_ver.dll, beside which ReShade's DXGI hooks failed and the game never started. The install used to
/// stop there; now, on every route, it moves his runtime to the backup and uninstall puts it back, and the
/// runtime copies the add-on makes of its own are never taken for his (Rollout, 28/09). Stand-ins only.</summary>
public class AuthorsRuntimeTests
{
    /// <summary>A game folder with his runtime (a build the pins know) under another name, a small loader
    /// that names it, and his setup's files.</summary>
    private static (string Game, byte[] Runtime, byte[] Loader, UserRuntime Build) RenamedByALoader(string tag, bool x64 = true)
    {
        var runtime = UserRuntimeTests.Dll(21);
        var loader = Encoding.Unicode.GetBytes("shim attach\0dlssnr_ver.dll\0").Concat(Fixture.Pe(true)).ToArray();
        var game = Fixture.Temp(tag);
        File.WriteAllBytes(Path.Combine(game, "Game.exe"), Fixture.Pe(x64));
        File.WriteAllBytes(Path.Combine(game, "dlssnr_ver.dll"), runtime);
        File.WriteAllBytes(Path.Combine(game, "version.dll"), loader);
        File.WriteAllText(Path.Combine(game, "dlssnr_on_amd_setup.exe"), "his setup");
        File.WriteAllText(Path.Combine(game, "dlssnr_on_amd.ini"), "[DlssNrOnAmd]\r\nEnabled=1\r\n");
        return (game, runtime, loader, UserRuntimeTests.Build(runtime));
    }

    private sealed record Route(string Name, Func<string, UserRuntime, (Func<Report> Install, Func<Report> Uninstall)> Make, bool X64 = true);

    private static readonly Route[] Routes =
    [
        new("D3D12", (game, build) =>
        {
            var (src, pins) = UserRuntimeTests.Payloads("ar-dx12", build);
            return (() => Work.Install(game, src, Preset.Dx12, pins), () => Work.Uninstall(game, Preset.Dx12));
        }),
        new("D3D11", (game, build) =>
        {
            var (src, pins) = UserRuntimeTests.Payloads("ar-dx11", build);
            return (() => Work.Install(game, src, Preset.Dx11, pins), () => Work.Uninstall(game, Preset.Dx11));
        }),
        new("32-bit D3D9", (game, build) =>
        {
            var (app, pinned) = UninstallInvariantTests.X86Release("ar-x86");
            var pins = new PayloadPins
            {
                AddonSha = "", AddonSize = 0, RuntimeSha = app.RuntimeSha, WeightsSha = app.WeightsSha,
                ReShade32Sha = app.ReShadeSha, D3d8To9Sha = app.D3d8To9Sha, BridgeVersion = "0.7.0", UserRuntimes = [build],
            };
            var exe = Path.Combine(game, "Game.exe");
            return (() => Work.Install(exe, app.Release, Preset.X86Dx9, pins), () => Work.Uninstall(exe, Preset.X86Dx9, pinned: pinned));
        }, X64: false),
        new("OptiScaler", (game, build) =>
        {
            var (src, p) = OptiScalerRouteTests.Payloads("ar-opti");
            var pins = new PayloadPins
            {
                AddonSha = p.AddonSha, AddonSize = p.AddonSize, WeightsSha = p.WeightsSha, WeightsSize = p.WeightsSize,
                OptiFiles = p.OptiFiles, OptiRuntimeName = p.OptiRuntimeName, OptiRuntimeSha = p.OptiRuntimeSha,
                OptiRuntimeSize = p.OptiRuntimeSize, UserRuntimes = [build],
            };
            return (() => Work.Install(game, src, Preset.OptiScaler, pins), () => Work.Uninstall(game, Preset.OptiScaler));
        }),
    ];

    [Fact]
    public void HisRuntimeUnderAnotherNameGoesToTheBackupOnEveryRouteAndComesBack()
    {
        foreach (var route in Routes)
        {
            var (game, runtime, loader, build) = RenamedByALoader($"ar-{route.Name}", route.X64);
            var (install, uninstall) = route.Make(game, build);
            var ver = Path.Combine(game, "dlssnr_ver.dll");

            // Still a source for the supporter build before the install moves it, which is how the app keeps it.
            Assert.Equal(runtime, File.ReadAllBytes(Work.FindUserRuntime(build, game)!));

            var report = install();
            Assert.False(report.Failed, report.ToLog($"{route.Name}: install"));
            Assert.True(Fixture.HasAny(report, "loaded here as dlssnr_ver.dll, by version.dll"), report.ToLog(route.Name));
            Assert.True(Fixture.HasAny(report, "moves dlssnr_ver.dll to the backup"), report.ToLog(route.Name));
            Assert.False(File.Exists(ver), $"{route.Name}: his runtime is out of the way");
            Assert.Equal(loader, File.ReadAllBytes(Path.Combine(game, "version.dll")));

            var removed = uninstall();
            Assert.False(removed.Failed, removed.ToLog($"{route.Name}: uninstall"));
            Assert.Equal(runtime, File.ReadAllBytes(ver));
            Assert.Equal(loader, File.ReadAllBytes(Path.Combine(game, "version.dll")));
            Assert.False(GameScanner.IsInstalled(game), $"{route.Name}: nothing of ours is left installed");
        }
    }

    /// <summary>Put back by hand while installed -- the original, or another file -- is not written over, and
    /// does not leave the folder counting as installed.</summary>
    [Fact]
    public void HisRuntimePutBackByHandIsLeftAsItIs()
    {
        foreach (var original in new[] { true, false })
        {
            var (game, runtime, _, build) = RenamedByALoader($"ar-back-{original}");
            var (src, pins) = UserRuntimeTests.Payloads("ar-back", build);
            Assert.False(Work.Install(game, src, Preset.Dx11, pins).Failed);
            var ver = Path.Combine(game, "dlssnr_ver.dll");
            var theirs = original ? runtime : Encoding.UTF8.GetBytes("somebody else's dll");
            File.WriteAllBytes(ver, theirs);

            var removed = Work.Uninstall(game, Preset.Dx11);
            Assert.False(removed.Failed, removed.ToLog("uninstall"));
            Assert.Equal(theirs, File.ReadAllBytes(ver));
            Assert.False(GameScanner.IsInstalled(game));
        }
    }

    /// <summary>The runtime copies the add-on makes beside the game, one per pass, and a copy patched for the
    /// add-on under any name are ours: not his standalone runtime, and uninstall takes the copies.</summary>
    [Fact]
    public void TheAddOnsOwnRuntimeCopiesAreNotHis()
    {
        var game = Fixture.Temp("ar-copies");
        File.WriteAllBytes(Path.Combine(game, "Game.exe"), Fixture.Pe(true));
        var original = UserRuntimeTests.Dll(23);
        var build = UserRuntimeTests.Build(original);
        var (src, pins) = UserRuntimeTests.Payloads("ar-copies", build);
        File.WriteAllBytes(Path.Combine(game, "amd-nr-pass2.dll"), original);
        File.WriteAllBytes(Path.Combine(game, "amd-nr-pass3.dll"), original);
        File.WriteAllBytes(Path.Combine(game, "somewhere.dll"), build.Patched(original));

        var check = Work.Preflight(game, src, Preset.Dx12, pins);
        Assert.False(Fixture.HasAny(check, "standalone runtime"), check.ToLog("preflight"));
        var report = Work.Install(game, src, Preset.Dx12, pins);
        Assert.False(report.Failed, report.ToLog("install"));
        Assert.False(Fixture.HasAny(report, "standalone runtime"), report.ToLog("install"));

        Assert.False(Work.Uninstall(game, Preset.Dx12).Failed);
        Assert.False(File.Exists(Path.Combine(game, "amd-nr-pass2.dll")));
        Assert.False(File.Exists(Path.Combine(game, "amd-nr-pass3.dll")));
    }

    /// <summary>His setup's files with none of his runtime loaded: a warning, and the install goes on.</summary>
    [Fact]
    public void HisSetupsFilesAloneAreAWarning()
    {
        var game = Fixture.Temp("ar-markers");
        File.WriteAllBytes(Path.Combine(game, "Game.exe"), Fixture.Pe(true));
        File.WriteAllText(Path.Combine(game, "dlssnr_on_amd_setup.exe"), "his setup");
        File.WriteAllText(Path.Combine(game, "dlssnr_on_amd.ini"), "[DlssNrOnAmd]\r\n");
        var (src, pins) = UserRuntimeTests.Payloads("ar-markers", UserRuntimeTests.Build(UserRuntimeTests.Dll(22)));

        var check = Work.Preflight(game, src, Preset.Dx11, pins);
        Assert.False(check.Failed, check.ToLog("markers"));
        Assert.Contains(check.Lines, l => l.Level == Level.Warn
            && l.Text.StartsWith("dlssnr_on_amd_setup.exe, dlssnr_on_amd.ini are here: danielblnc's own setup", StringComparison.Ordinal));
        var report = Work.Install(game, src, Preset.Dx11, pins);
        Assert.False(report.Failed, report.ToLog("markers install"));
        Assert.True(File.Exists(Path.Combine(game, Work.AddonName)));

        // Once a runtime of ours is here, the ini is what that runtime writes, and says nothing about his setup.
        File.Delete(Path.Combine(game, "dlssnr_on_amd_setup.exe"));
        Assert.DoesNotContain(Work.Preflight(game, src, Preset.Dx11, pins).Lines, l => l.Text.Contains("his own setup"));
    }

    /// <summary>The mochizuki runtime and its model copied in by hand, as Rollout had them: uninstall takes them
    /// when they are the builds this app pins, and leaves them to OptiScaler when it is here.</summary>
    [Fact]
    public void AMochizukiNothingRecordedGoesWhenItIsThePinnedBuild()
    {
        foreach (var opti in new[] { false, true })
        {
            var game = Fixture.Temp($"ar-mz-{opti}");
            File.WriteAllBytes(Path.Combine(game, "Game.exe"), Fixture.Pe(true));
            string[] names = ["MochizukiNrRuntime.dll", "dlssnr-amd/dlssnr.bin", "dlssnr-amd/shaders/g_attn.spv"];
            var pinned = new List<string>();
            foreach (var name in names)
            {
                var path = Path.Combine(game, name);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, $"pinned {name}");
                pinned.Add(Engine.HashFile(path));
            }
            Directory.CreateDirectory(Path.Combine(game, "dlssnr-amd", "prewarm"));
            File.WriteAllText(Path.Combine(game, "dlssnr-amd", "prewarm", "manifest.txt"), "rewritten for this driver");
            if (opti) File.WriteAllText(Path.Combine(game, Work.OptiScalerIni), "[DlssNr]\n");

            var report = Work.Uninstall(game, Preset.Dx11, pinned: pinned);
            Assert.False(report.Failed, report.ToLog("uninstall"));
            Assert.Equal(opti, File.Exists(Path.Combine(game, "MochizukiNrRuntime.dll")));
            Assert.Equal(opti, Directory.Exists(Path.Combine(game, "dlssnr-amd")));
        }
    }
}
