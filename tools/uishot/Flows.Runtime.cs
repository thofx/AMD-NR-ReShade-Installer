// A runtime build the person supplies, as they meet it: listed in the payload for the add-on version
// installed, the sheet offers it after the download; picked with no copy on this machine, the check says
// to supply it; with danielblnc's version.dll in the game that is the copy, kept for other games, and
// Install puts it in patched and takes version.dll to the backup; the folder is not out of date over it,
// and picking the download again puts the download back. Run by the flows in Program.cs with the
// ReShade route installed at add-on 1.0.1. The file picker is the one step a headless window cannot take.

using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using AmdNr.App;
using AmdNr.Core;

internal static class RuntimeFlow
{
    public static void Run(MainWindow main, GameSheet sheet, GameCard card, string game,
        Action<bool, string> check, Func<Func<bool>, int, bool> until, Action<Button> click, Action<Window, string> save)
    {
        string S(string key) => main.FindResource(key) as string ?? key;
        T Named<T>(string name) where T : Control => sheet.FindControl<T>(name)!;
        var section = Named<StackPanel>("RuntimeSection");
        var box = Named<ComboBox>("RuntimeBox");
        var file = Named<Button>("RuntimeFileButton");
        var note = Named<TextBlock>("RuntimeNote");
        var details = Named<ItemsControl>("ReportList");
        var install = Named<Button>("InstallButton");
        var runtime = Path.Combine(game, Work.RuntimeName);
        var missing = string.Format(S("Str.RuntimeMissing"), "9.9.9");
        bool Says(string text) => details.Items.OfType<ReportLine>().Any(l => l.Text == text);

        check(!section.IsVisible, "with no build to supply in the payload list, there is no choice");
        var (sha, patched, original) = Seed();
        var reload = main.Session.LoadManifestAsync();
        check(until(() => reload.IsCompleted && section.IsVisible && box.ItemCount == 2, 10),
            "a build the add-on runs is offered after the download");
        check(box.SelectedIndex == 0 && !file.IsVisible && card.Entry.UserRuntime is null, "the download, until picked");

        box.SelectedIndex = 1;
        check(card.Entry.UserRuntime == sha && file.IsVisible, "picking it is remembered for the game, and offers the file");
        check(until(() => Says(missing), 10), "with no copy on this machine, the check says to supply it");
        until(() => false, 1);
        if (section.FindAncestorOfType<ScrollViewer>() is { Content: Visual content } scroll
            && section.TranslatePoint(new Point(0, 0), content) is { } at)
            scroll.Offset = new Vector(0, Math.Max(0, at.Y - 160));
        until(() => false, 1);
        save(main, "flow-3-runtime-missing");

        // His setup ran in this game: his version.dll is the copy, and the app keeps it.
        File.WriteAllBytes(Path.Combine(game, "version.dll"), original);
        box.SelectedIndex = 0;
        box.SelectedIndex = 1;
        check(until(() => note.Text?.Contains(S("Str.RuntimeKeptNote")) == true && !Says(missing), 10),
            "danielblnc's version.dll in the game is found and kept");
        click(install);
        check(until(() => !main.Session.Busy, 30) && Engine.HashFile(runtime) == patched
              && !File.Exists(Path.Combine(game, "version.dll")),
            "Install puts the build in patched, and version.dll in the backup");
        check(!card.Outdated, "and the folder is not out of date over the runtime it chose");

        box.SelectedIndex = 0;
        check(card.Entry.UserRuntime == "", "the download picked again is remembered too");
        until(() => false, 1);
        click(install);
        check(until(() => !main.Session.Busy, 30) && Engine.HashFile(runtime) != patched,
            "and Install puts the download back");
    }

    /// <summary>A stand-in build listed under user_runtimes, run by add-ons from 1.0.0 on, with one change.</summary>
    private static (string Sha, string Patched, byte[] Original) Seed()
    {
        var original = new byte[8192];
        new Random(5).NextBytes(original);
        var patched = (byte[])original.Clone();
        new byte[] { 0x90, 0x90, 0x90, 0x90 }.CopyTo(patched, 0x100);
        var path = Path.Combine(AppPaths.Root, "payload.json");
        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        root["user_runtimes"] = new JsonArray(new JsonObject
        {
            ["runtime"] = "DLSS-NR-on-AMD stand-in",
            ["name"] = "9.9.9",
            ["addon_since"] = "1.0.0",
            ["original_sha256"] = Engine.Sha(original),
            ["original_size"] = original.Length,
            ["patched_sha256"] = Engine.Sha(patched),
            ["changes"] = new JsonArray(new JsonObject
            {
                ["patch"] = "setup-thread",
                ["offset"] = "0x100",
                ["before"] = Convert.ToHexStringLower(original.AsSpan(0x100, 4)),
                ["after"] = "90909090",
            }),
        });
        File.WriteAllText(path, root.ToJsonString());
        return (Engine.Sha(original), Engine.Sha(patched), original);
    }
}
