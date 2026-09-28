// Which danielblnc runtime the install takes: the download, or a build the person supplies. Some of his
// builds go to his supporters only and are not distributed, by this app or anyone behind it; somebody
// who has one points the sheet at their own version.dll or setup, and the app keeps the checked
// original so other games do not ask again. Offered only where the route and version chosen run it.

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AmdNr.Core;

namespace AmdNr.App;

public partial class GameSheet
{
    /// <summary>The builds the route and version on screen run, in the menu after the download.</summary>
    private IReadOnlyList<UserRuntime> _runtimes = [];

    /// <summary>Whether the last check found a copy of the wanted build on this machine.</summary>
    private bool _runtimeFound;

    private void ShowRuntime()
    {
        if (_card is not { } card) return;
        _runtimes = Work.OfferedRuntimes(Pins(), card.Entry.Preset);
        RuntimeSection.IsVisible = _runtimes.Count > 0;
        if (_runtimes.Count == 0) return;

        var wanted = WantedRuntime(card);
        var index = wanted is null ? -1 : _runtimes.ToList().FindIndex(b => b.OriginalSha256 == wanted.OriginalSha256);
        _setting = true;
        RuntimeBox.ItemsSource = _runtimes.Select(b => Ui.Format("Str.RuntimeOwn", b.Name))
            .Prepend(Ui.Text("Str.RuntimeDownload")).ToList();
        RuntimeBox.SelectedIndex = index + 1;
        _setting = false;
        RuntimeFileButton.IsVisible = index >= 0;
        RuntimeNote.Text = RuntimeNoteText();
    }

    private string RuntimeNoteText()
    {
        if (RuntimeBox.SelectedIndex is var i && (i < 1 || i > _runtimes.Count)) return Ui.Text("Str.RuntimeNoteDownload");
        var build = _runtimes[i - 1];
        return Ui.Format("Str.RuntimeNoteOwn", build.Name) + " "
               + Ui.Text(_card?.Entry.Preset.IsOptiScaler() == true ? "Str.RuntimeNoteOpti" : "Str.RuntimeNoteAddon")
               + (_runtimeFound ? " " + Ui.Text("Str.RuntimeKeptNote") : "");
    }

    /// <summary>The build this game's install takes in place of the download, whether or not the version
    /// on screen runs it -- the engine then installs the download and says why: the one the person picked,
    /// or, until they pick, the one the folder runs. Null is the download. Read off the folder the way the
    /// mochizuki box is, and a folder that cannot be read is the download.</summary>
    private UserRuntime? WantedRuntime(GameCard card)
    {
        var builds = Pins().UserRuntimes;
        if (card.Entry.UserRuntime is { } chosen) return builds.FirstOrDefault(b => b.OriginalSha256 == chosen);
        try { return Work.UserRuntimeIn(TargetFor(card), builds); }
        catch (Exception) { return null; }
    }

    private bool Offered(UserRuntime build) => _runtimes.Any(b => b.OriginalSha256 == build.OriginalSha256);

    /// <summary>After a check: says under the menu whether this machine has the build, and, when it is
    /// wanted, offered and nowhere to be found, puts that in the pre-flight as the error it is.</summary>
    private void NoteRuntime(Report report, UserRuntime? wanted, string? source)
    {
        _runtimeFound = source is not null;
        if (_runtimes.Count > 0) RuntimeNote.Text = RuntimeNoteText();
        if (wanted is not null && source is null && Offered(wanted)) report.Err(Ui.Format("Str.RuntimeMissing", wanted.Name));
    }

    private void OnRuntimeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_setting || _card is not { } card) return;
        var i = RuntimeBox.SelectedIndex;
        card.Entry.UserRuntime = i >= 1 && i <= _runtimes.Count ? _runtimes[i - 1].OriginalSha256 : "";
        Library.Save();
        ShowRuntime();
        // Different bytes to install, and maybe a file to ask for: the pre-flight says so again.
        _ = RefreshAsync();
    }

    /// <summary>The person's own file: danielblnc's version.dll, or the setup it came in. It is checked
    /// against the builds the payload list names and kept, so no other game asks for it again.</summary>
    private void OnChooseRuntimeFile(object? sender, RoutedEventArgs e) => _shell.Run("choose runtime", async () =>
    {
        if (_card is not { } card || Session.Busy) return;
        var picked = await _shell.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Ui.Text("Str.RuntimeChooseTitle"),
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("version.dll, dlssnr_on_amd_setup.exe") { Patterns = ["*.dll", "*.exe"] }],
        });
        var path = picked.Count > 0 ? picked[0].TryGetLocalPath() : null;
        if (string.IsNullOrWhiteSpace(path)) return;

        var builds = Pins().UserRuntimes;
        UserRuntime build;
        try { build = await Task.Run(() => UserRuntime.Keep(path, builds)); }
        catch (InstallException ex)
        {
            _shell.Toast(Ui.Format("Str.RuntimeRefused", ex.Message), Level.Err);
            return;
        }
        card.Entry.UserRuntime = build.OriginalSha256;
        Library.Save();
        _shell.Toast(Ui.Format("Str.RuntimeKept", build.Name), Level.Ok);
        ShowRuntime();
        _ = RefreshAsync();
    });
}
