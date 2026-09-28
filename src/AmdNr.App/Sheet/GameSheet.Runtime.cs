// danielblnc's supporter build: a newer runtime than the one this app downloads, which is not distributed,
// by this app or anyone behind it. A supporter hands the sheet their own version.dll or setup, the app keeps
// the checked original so other games do not ask again, and says which runtime goes in. The block shows on
// every ReShade route while the payload list names a build, and on OptiScaler where a release runs one.

using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AmdNr.Core;

namespace AmdNr.App;

public partial class GameSheet
{
    /// <summary>Whether the last check found a copy of the wanted build on this machine.</summary>
    private bool _runtimeFound;

    private void ShowRuntime()
    {
        if (_card is not { } card) return;
        var opti = card.Entry.Preset.IsOptiScaler();
        var listed = Pins().UserRuntimes.Where(b => !opti || Work.OptiScalerSince(b.OriginalSha256) is not null)
            .OrderByDescending(b => AddonReleases.Version(b.Name)).ToList();
        RuntimeSection.IsVisible = listed.Count > 0;
        if (listed.Count == 0) return;

        var wanted = WantedRuntime(card, out _);
        var build = wanted ?? listed[0];
        RuntimePitch.Text = Ui.Text("Str.SupporterPitch") + " " + Ui.Translated($"Str.SupporterPitch.{build.Name}", "");
        RuntimeDownloadButton.IsVisible = wanted is not null;
        _runtimeFound = wanted?.Kept() is not null;
        RuntimeNote.Text = RuntimeStatus(card);
    }

    /// <summary>Which runtime goes in, said plainly under the button.</summary>
    private string RuntimeStatus(GameCard card)
    {
        if (WantedRuntime(card, out var preselected) is not { } build) return Ui.Text("Str.SupporterUsingDownload");
        var pins = Pins();
        if (!Offered(build))
            return card.Entry.Preset.IsOptiScaler()
                ? Ui.Format("Str.SupporterNeedsOpti", build.Name, Work.OptiScalerSince(build.OriginalSha256))
                : !build.Patchable ? Ui.Format("Str.SupporterNoPatch", build.Name)
                : Ui.Format("Str.SupporterNeedsAddon", build.Name, build.AddonSince);
        if (!_runtimeFound) return Ui.Format("Str.SupporterMissing", build.Name);
        return Ui.Format(preselected ? "Str.SupporterUsingKept" : "Str.SupporterUsingFile", build.Name);
    }

    /// <summary>The build this game's install takes in place of the download, whether or not the version
    /// on screen runs it -- the engine then installs the download and says why: the one the person picked,
    /// or, until they pick, the one the folder runs, or else the newest this version runs that this machine
    /// kept a copy of (<paramref name="preselected"/>). Null is the download. Read off the folder the way the
    /// mochizuki box is, and a folder that cannot be read is the download.</summary>
    private UserRuntime? WantedRuntime(GameCard card, out bool preselected)
    {
        preselected = false;
        var builds = Pins().UserRuntimes;
        if (card.Entry.UserRuntime is { } chosen) return builds.FirstOrDefault(b => b.OriginalSha256 == chosen);
        try
        {
            if (Work.UserRuntimeIn(TargetFor(card), builds) is { } there) return there;
            var kept = Work.OfferedRuntimes(Pins(), card.Entry.Preset).Where(b => b.Kept() is not null)
                .MaxBy(b => AddonReleases.Version(b.Name));
            preselected = kept is not null;
            return kept;
        }
        catch (Exception) { return null; }
    }

    private UserRuntime? WantedRuntime(GameCard card) => WantedRuntime(card, out _);

    private bool Offered(UserRuntime build) =>
        _card is { } card && Work.OfferedRuntimes(Pins(), card.Entry.Preset).Any(b => b.OriginalSha256 == build.OriginalSha256);

    /// <summary>After a check: says under the button whether this machine has the build, and, when it is
    /// wanted, offered and nowhere to be found, puts that in the pre-flight as the error it is.</summary>
    private void NoteRuntime(Report report, UserRuntime? wanted, string? source)
    {
        _runtimeFound = source is not null;
        if (_card is { } card && RuntimeSection.IsVisible) RuntimeNote.Text = RuntimeStatus(card);
        if (wanted is not null && source is null && Offered(wanted)) report.Err(Ui.Format("Str.SupporterMissing", wanted.Name));
    }

    /// <summary>Back to the download, for this game, until the person picks their files again.</summary>
    private void OnUseDownload(object? sender, RoutedEventArgs e)
    {
        if (_card is not { } card || Session.Busy) return;
        card.Entry.UserRuntime = "";
        Library.Save();
        ShowRuntime();
        _ = RefreshAsync();
    }

    /// <summary>The person's own file: danielblnc's version.dll, or the setup it came in. It is checked
    /// against the builds the payload list names and kept, so no other game asks for it again.</summary>
    private void OnChooseRuntimeFile(object? sender, RoutedEventArgs e) => _shell.Run("choose runtime", async () =>
    {
        if (_card is not { } card || Session.Busy) return;
        var picked = await _shell.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Ui.Text("Str.SupporterPickTitle"),
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(Ui.Text("Str.SupporterFiles")) { Patterns = ["version.dll", "dlssnr_on_amd_setup.exe"] },
                new FilePickerFileType(Ui.Text("Str.SupporterAnyFile")) { Patterns = ["*.dll", "*.exe"] },
            ],
        });
        var path = picked.Count > 0 ? picked[0].TryGetLocalPath() : null;
        if (string.IsNullOrWhiteSpace(path)) return;

        var builds = Pins().UserRuntimes;
        UserRuntime build;
        try { build = await Task.Run(() => UserRuntime.Keep(path, builds)); }
        catch (InstallException ex)
        {
            _shell.Toast(Ui.Format("Str.SupporterRefused", ex.Message), Level.Err);
            return;
        }
        card.Entry.UserRuntime = build.OriginalSha256;
        Library.Save();
        _shell.Toast(Ui.Format("Str.SupporterKept", build.Name), Level.Ok);
        ShowRuntime();
        _ = RefreshAsync();
    });
}
