using System.Text;
using AmdNr.Core;

namespace AmdNr.Core.Tests;

/// <summary>danielblnc's standalone runtime left in a game folder by his setup, found by what it hashes to
/// rather than by its name. NBA 2K27 is the case: a chain loader as version.dll loading his runtime renamed
/// dlssnr_ver.dll, beside which ReShade's DXGI hooks failed and the game never started. Stand-ins only.</summary>
public class AuthorsRuntimeTests
{
    /// <summary>A game folder with his runtime (a build the pins know) under another name, a small loader
    /// that names it, and his setup's files.</summary>
    private static (string Game, byte[] Runtime, byte[] Loader, UserRuntime Build) RenamedByALoader(string tag)
    {
        var runtime = UserRuntimeTests.Dll(21);
        var loader = Encoding.Unicode.GetBytes("shim attach\0dlssnr_ver.dll\0").Concat(Fixture.Pe(true)).ToArray();
        var game = Fixture.Temp(tag);
        File.WriteAllBytes(Path.Combine(game, "Game.exe"), Fixture.Pe(true));
        File.WriteAllBytes(Path.Combine(game, "dlssnr_ver.dll"), runtime);
        File.WriteAllBytes(Path.Combine(game, "version.dll"), loader);
        File.WriteAllText(Path.Combine(game, "dlssnr_on_amd_setup.exe"), "his setup");
        File.WriteAllText(Path.Combine(game, "dlssnr_on_amd.ini"), "[DlssNrOnAmd]\r\nEnabled=1\r\n");
        return (game, runtime, loader, UserRuntimeTests.Build(runtime));
    }

    [Fact]
    public void HisRuntimeUnderAnotherNameStopsAReShadeInstallAndIsLeftAlone()
    {
        var (game, runtime, loader, build) = RenamedByALoader("ar-renamed");
        var (src, pins) = UserRuntimeTests.Payloads("ar-renamed", build);

        var check = Work.Preflight(game, src, Preset.Dx12, pins);
        Assert.True(check.Failed);
        Assert.True(Fixture.HasErr(check, "danielblnc's standalone runtime 9.9.9 is loaded here by version.dll, as dlssnr_ver.dll"),
            check.ToLog("preflight"));
        Assert.True(Fixture.HasErr(check, "same DXGI and D3D12 calls ReShade does"), check.ToLog("preflight"));
        Assert.True(Fixture.HasErr(check, "dlssnr_ver.dll is his 9.9.9 supporter build: pick it"), check.ToLog("preflight"));

        foreach (var preset in new[] { Preset.Dx12, Preset.Dx11 })
        {
            var report = Work.Install(game, src, preset, pins);
            Assert.True(report.Failed, report.ToLog("install"));
            Assert.False(File.Exists(Path.Combine(game, Work.AddonName)));
            Assert.False(File.Exists(Path.Combine(game, Route.X64.ManifestFileName())));
        }
        Assert.Equal(runtime, File.ReadAllBytes(Path.Combine(game, "dlssnr_ver.dll")));
        Assert.Equal(loader, File.ReadAllBytes(Path.Combine(game, "version.dll")));

        // The renamed file is still a source for the supporter build, which is how the person keeps it.
        var found = Work.FindUserRuntime(build, game);
        Assert.NotNull(found);
        Assert.Equal(runtime, File.ReadAllBytes(found!));
    }

    /// <summary>The same folder on the OptiScaler route: said, and nothing else changes.</summary>
    [Fact]
    public void OnOptiScalerItIsSaidAndNothingElseChanges()
    {
        var (game, runtime, loader, build) = RenamedByALoader("ar-opti");
        var (src, pins) = OptiScalerRouteTests.Payloads("ar-opti");
        pins = new PayloadPins
        {
            AddonSha = pins.AddonSha, AddonSize = pins.AddonSize, WeightsSha = pins.WeightsSha, WeightsSize = pins.WeightsSize,
            OptiFiles = pins.OptiFiles, OptiRuntimeName = pins.OptiRuntimeName, OptiRuntimeSha = pins.OptiRuntimeSha,
            OptiRuntimeSize = pins.OptiRuntimeSize, UserRuntimes = [build],
        };
        var report = Work.Install(game, src, Preset.OptiScaler, pins);
        Assert.False(report.Failed, report.ToLog("opti"));
        Assert.Contains(report.Lines, l => l.Level == Level.Warn && l.Text.Contains("loaded here by version.dll, as dlssnr_ver.dll"));
        Assert.Equal(runtime, File.ReadAllBytes(Path.Combine(game, "dlssnr_ver.dll")));
        Assert.Equal(loader, File.ReadAllBytes(Path.Combine(game, "version.dll")));
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
}
