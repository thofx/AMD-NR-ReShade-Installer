// danielblnc's runtime for this game: the download, or his supporter build -- a newer runtime than the one
// this app downloads, which is not distributed, by this app or anyone behind it. Two cards, one lit: picking
// the supporter card takes the copy this machine already kept, or asks for the person's own version.dll or
// setup right there; the app keeps the checked original so other games do not ask again, and says under the
// card which runtime goes in. The block shows on every ReShade route while the payload list names a build,
// and on OptiScaler where a release runs one.

using Avalonia.Controls;
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
        RuntimeDownloadTitle.Text = DownloadVersion(card) is { Length: > 0 } version
            ? Ui.Format("Str.SupporterDownloadTitle", version)
            : Ui.Text("Str.SupporterUseDownload");
        RuntimeSupporterTitle.Text = Ui.Format("Str.SupporterBuildTitle", build.Name);
        RuntimePitch.Text = Ui.Text("Str.SupporterPitch") + " " + Ui.Translated($"Str.SupporterPitch.{build.Name}", "");
        _setting = true;
        RuntimeDownloadChoice.IsChecked = wanted is null;
        RuntimeSupporterChoice.IsChecked = wanted is not null;
        _setting = false;
        RuntimeSupporterPanel.IsVisible = wanted is not null;
        _runtimeFound = wanted?.Kept() is not null;
        ShowRuntimeNote(card);
    }

    /// <summary>The version the download card names: the runtime the route installs when nobody supplies one.</summary>
    private string? DownloadVersion(GameCard card)
    {
        if (card.Entry.Preset.IsOptiScaler()) return Pins().OptiRuntimeVersion;
        return Selected() is { } manifest && manifest.Has(PayloadManifest.RuntimeComponent)
            ? manifest.Component(PayloadManifest.RuntimeComponent).Version
            : null;
    }

    /// <summary>Under the supporter card: which runtime goes in, green when it is the build, amber when
    /// something is missing, and the file button worded for whether a copy is already here.</summary>
    private void ShowRuntimeNote(GameCard card)
    {
        var (level, text) = RuntimeStatus(card);
        RuntimeNote.Text = text;
        RuntimeNote.Foreground = Ui.LevelBrush(level);
        RuntimeNoteIcon.Data = Ui.Glyph(level);
        RuntimeNoteIcon.Foreground = Ui.LevelBrush(level);
        RuntimeFileLabel.Text = Ui.Text(_runtimeFound ? "Str.SupporterPickOther" : "Str.SupporterPick");
    }

    /// <summary>Which runtime goes in, said plainly under the supporter card.</summary>
    private (Level Level, string Text) RuntimeStatus(GameCard card)
    {
        if (WantedRuntime(card, out var preselected) is not { } build) return (Level.Info, Ui.Text("Str.SupporterUsingDownload"));
        if (!Offered(build))
            return (Level.Warn, card.Entry.Preset.IsOptiScaler()
                ? Ui.Format("Str.SupporterNeedsOpti", build.Name, Work.OptiScalerSince(build.OriginalSha256))
                : !build.Patchable ? Ui.Format("Str.SupporterNoPatch", build.Name)
                : Ui.Format("Str.SupporterNeedsAddon", build.Name, build.AddonSince));
        if (!_runtimeFound) return (Level.Warn, Ui.Format("Str.SupporterMissing", build.Name));
        return (Level.Ok, Ui.Format(preselected ? "Str.SupporterUsingKept" : "Str.SupporterUsingFile", build.Name));
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

    /// <summary>After a check: says under the card whether this machine has the build, and, when it is
    /// wanted, offered and nowhere to be found, puts that in the pre-flight as the error it is.</summary>
    private void NoteRuntime(Report report, UserRuntime? wanted, string? source)
    {
        _runtimeFound = source is not null;
        if (_card is { } card && RuntimeSection.IsVisible) ShowRuntimeNote(card);
        if (wanted is not null && source is null && Offered(wanted)) report.Err(Ui.Format("Str.SupporterMissing", wanted.Name));
    }

    /// <summary>A card picked. The download is remembered for this game until the person picks the supporter
    /// card again; the supporter card takes a copy already on this machine -- the one in the folder, or one
    /// kept from an earlier pick -- and with none, asks for the file there and then.</summary>
    private void OnRuntimeChoice(object? sender, RoutedEventArgs e)
    {
        if (_setting || _card is not { } card || sender is not RadioButton { IsChecked: true } choice) return;
        if (Session.Busy)
        {
            ShowRuntime();
            return;
        }
        if (choice == RuntimeDownloadChoice)
        {
            ChooseRuntime(card, "");
            return;
        }
        var builds = Pins().UserRuntimes;
        UserRuntime? ready;
        try
        {
            ready = Work.UserRuntimeIn(TargetFor(card), builds)
                    ?? builds.Where(b => b.Kept() is not null).MaxBy(b => AddonReleases.Version(b.Name));
        }
        catch (Exception) { ready = null; }
        if (ready is not null) ChooseRuntime(card, ready.OriginalSha256);
        else OnChooseRuntimeFile(sender, e);
    }

    private void ChooseRuntime(GameCard card, string runtime)
    {
        card.Entry.UserRuntime = runtime;
        Library.Save();
        ShowRuntime();
        _ = RefreshAsync();
    }

    /// <summary>The person's own file: danielblnc's version.dll, or the setup it came in. It is checked
    /// against the builds the payload list names and kept, so no other game asks for it again. Nothing
    /// picked, or a file refused, leaves the cards as they were.</summary>
    private void OnChooseRuntimeFile(object? sender, RoutedEventArgs e) => _shell.Run("choose runtime", async () =>
    {
        if (_card is not { } card || Session.Busy)
        {
            ShowRuntime();
            return;
        }
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
        if (string.IsNullOrWhiteSpace(path))
        {
            ShowRuntime();
            return;
        }

        var builds = Pins().UserRuntimes;
        UserRuntime build;
        try { build = await Task.Run(() => UserRuntime.Keep(path, builds)); }
        catch (InstallException ex)
        {
            _shell.Toast(Ui.Format("Str.SupporterRefused", ex.Message), Level.Err);
            ShowRuntime();
            return;
        }
        _shell.Toast(Ui.Format("Str.SupporterKept", build.Name), Level.Ok);
        ChooseRuntime(card, build.OriginalSha256);
    });
}
