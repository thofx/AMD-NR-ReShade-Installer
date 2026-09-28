// danielblnc's runtime builds a person brings themselves. Some of his builds go to his supporters
// only, and those are not distributed: not by this app, not by anyone behind it, now or later. What
// payload.json carries for one is how to recognise it -- its SHA-256 and its size -- and, for the
// add-on, the same-length changes that let the add-on drive it, written the way the add-on's
// tools/runtime-patches.json writes them. The person points the app at their own version.dll, or at
// the dlssnr_on_amd_setup.exe it came in, and the checked original is kept in the app's data folder
// under its hash, so other games do not ask again. Every install hashes it again.

using System.Globalization;
using System.Text.Json.Serialization;

namespace AmdNr.Core;

/// <summary>One change to a build, in place and the same length, so no RVA moves.</summary>
public sealed class RuntimeChange
{
    public required string Patch { get; init; }

    /// <summary>A file offset, in hex: "0x65bd".</summary>
    public required string Offset { get; init; }

    public required string Before { get; init; }
    public required string After { get; init; }
}

public sealed class UserRuntime
{
    /// <summary>What the build is, as runtime-patches.json names it: "DLSS-NR-on-AMD v0.5.0".</summary>
    public required string Runtime { get; init; }

    /// <summary>What the sheet and the report call it: "0.5.0".</summary>
    public required string Name { get; init; }

    /// <summary>The first add-on (and bridge) release that runs the build patched.</summary>
    [JsonPropertyName("addon_since")]
    public required string AddonSince { get; init; }

    [JsonPropertyName("original_sha256")]
    public required string OriginalSha256 { get; init; }

    [JsonPropertyName("original_size")]
    public required ulong OriginalSize { get; init; }

    /// <summary>What the build hashes to with <see cref="Changes"/> applied. Empty until it is known.</summary>
    [JsonPropertyName("patched_sha256")]
    public string PatchedSha256 { get; init; } = string.Empty;

    public List<RuntimeChange> Changes { get; init; } = [];

    /// <summary>Whether the add-on can be given the build: its changes and the hash they come out at are
    /// listed. Until they are, it is offered only where nothing is patched, on OptiScaler.</summary>
    [JsonIgnore]
    public bool Patchable => Changes.Count > 0 && Engine.IsHex(PatchedSha256, 64);

    /// <summary>Whether an add-on (or bridge) at this version runs the build.</summary>
    public bool RunsOn(string addonVersion) =>
        Patchable && AddonReleases.Version(addonVersion) is { } version && version >= AddonReleases.Version(AddonSince);

    /// <summary>The payload list is remote data: an entry that could not be applied as written is refused
    /// with the rest of the list, like any other.</summary>
    internal void Check()
    {
        Engine.Require(Name.Trim().Length > 0 && Runtime.Trim().Length > 0, "A user-supplied runtime has no name.");
        Engine.Require(Engine.IsHex(OriginalSha256, 64) && OriginalSize > 0,
            $"The user-supplied runtime {Name} has no usable SHA-256 or size.");
        Engine.Require(PatchedSha256.Length == 0 || Engine.IsHex(PatchedSha256, 64),
            $"The user-supplied runtime {Name} has an unusable patched SHA-256.");
        Engine.Require(AddonReleases.Version(AddonSince) is not null,
            $"The user-supplied runtime {Name} has an unusable addon_since: '{AddonSince}'");
        foreach (var change in Changes)
            Engine.Require(OffsetOf(change.Offset) is { } at && at < (long)OriginalSize
                           && Hex(change.Before) is { Length: > 0 } before && Hex(change.After) is { } after
                           && before.Length == after.Length && at + before.Length <= (long)OriginalSize,
                $"The user-supplied runtime {Name} has a change it cannot apply: {change.Patch} at {change.Offset}.");
    }

    /// <summary>The build the way the add-on drives it: every change applied only over the bytes it expects,
    /// and the result only when it hashes to <see cref="PatchedSha256"/>.</summary>
    public byte[] Patched(byte[] original)
    {
        Engine.Require(Engine.Sha(original) == OriginalSha256,
            $"That file is not danielblnc's runtime {Name}: its SHA-256 is not {OriginalSha256}.");
        Engine.Require(Patchable, $"No patch for danielblnc's runtime {Name} is listed yet, so the add-on cannot drive it.");
        var patched = Swapped(original, forward: true);
        var got = Engine.Sha(patched);
        Engine.Require(got == PatchedSha256,
            $"danielblnc's runtime {Name}, patched, hashes to {got} and not to the {PatchedSha256} the payload "
            + "list gives, so it is not the build the add-on runs.");
        return patched;
    }

    /// <summary>The original back out of the patched copy an install put in a game folder: the same changes,
    /// the other way.</summary>
    private byte[] Unpatched(byte[] patched)
    {
        var original = Swapped(patched, forward: false);
        Engine.Require(Engine.Sha(original) == OriginalSha256, $"That file is not danielblnc's runtime {Name}.");
        return original;
    }

    private byte[] Swapped(byte[] source, bool forward)
    {
        var bytes = (byte[])source.Clone();
        foreach (var change in Changes)
        {
            var at = (int)OffsetOf(change.Offset)!.Value;
            var (from, to) = forward ? (Hex(change.Before)!, Hex(change.After)!) : (Hex(change.After)!, Hex(change.Before)!);
            Engine.Require(at + from.Length <= bytes.Length && bytes.AsSpan(at, from.Length).SequenceEqual(from),
                $"The bytes at {change.Offset} in danielblnc's runtime {Name} are not the ones the {change.Patch} "
                + "change expects, so it is not applied.");
            to.CopyTo(bytes, at);
        }
        return bytes;
    }

    private static long? OffsetOf(string text) =>
        long.TryParse(text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text[2..] : text,
            NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var at) && at >= 0 ? at : null;

    private static byte[]? Hex(string text)
    {
        try { return Convert.FromHexString(text); }
        catch (FormatException) { return null; }
    }

    // -- What a person points the app at -----------------------------------------------------------

    /// <summary>The build a file holds, and its original bytes: danielblnc's version.dll, the patched copy an
    /// install of this app put in a game folder, or his dlssnr_on_amd_setup.exe with the build inside it.</summary>
    public static (UserRuntime Build, byte[] Original) Read(string path, IReadOnlyList<UserRuntime> builds)
    {
        var name = Path.GetFileName(path);
        Engine.Require(builds.Count > 0, "The payload list this app read names no runtime build to supply yourself.");
        var bytes = Engine.Read(path);
        var sha = Engine.Sha(bytes);
        foreach (var build in builds)
        {
            if (sha == build.OriginalSha256) return (build, bytes);
            if (build.Patchable && sha == build.PatchedSha256) return (build, build.Unpatched(bytes));
        }
        var listed = string.Join(" or ", builds.Select(b => b.Name));
        if (Extract(bytes) is { } inner)
        {
            var innerSha = Engine.Sha(inner);
            return builds.FirstOrDefault(b => b.OriginalSha256 == innerSha) is { } found
                ? (found, inner)
                : throw new InstallException(
                    $"{name} carries danielblnc's runtime with the SHA-256 {innerSha}, which is not {listed}.");
        }
        throw new InstallException(
            $"{name} is neither danielblnc's runtime {listed} nor his setup with it inside: its SHA-256 is {sha}.");
    }

    /// <summary>danielblnc's runtime out of his dlssnr_on_amd_setup.exe, without running it (ported from the
    /// add-on's tools/extract_runtime.py). Since v0.3.3 the setup carries the DLL as a byte array in its .rdata,
    /// at an offset that moves, so neither the file's size nor the setup's own end says where it is: the DLL
    /// does. Every "MZ" whose header leads to a PE signature, says DLL and has its sections end inside the file
    /// is a candidate, and there has to be exactly one besides the setup at offset 0 -- two means the layout
    /// moved again, and guessing between them pins a wrong hash. It ends at the highest raw pointer plus raw
    /// size across its own section table. Null when the file is not a setup like that.</summary>
    internal static byte[]? Extract(byte[] data)
    {
        if (PeImage(data, 0) is null) return null;
        (int Start, long End)? found = null;
        for (var at = IndexOfMz(data, 1); at >= 0; at = IndexOfMz(data, at + 1))
        {
            if (PeImage(data, at) is not { Dll: true } image || image.End > data.Length) continue;
            if (found is not null) return null;
            found = (at, image.End);
        }
        return found is { } f ? data[f.Start..(int)f.End] : null;

        static int IndexOfMz(byte[] data, int from) =>
            from >= data.Length ? -1 : data.AsSpan(from).IndexOf("MZ"u8) is var i and >= 0 ? from + i : -1;
    }

    /// <summary>Where the PE at <paramref name="at"/> ends (the highest raw end of its sections, not the
    /// last one in table order) and whether it is a DLL; null when the bytes there are not a PE header,
    /// which most "MZ" in a binary are not.</summary>
    private static (long End, bool Dll)? PeImage(byte[] data, int at)
    {
        if ((long)at + 0x40 > data.Length || data[at] != 'M' || data[at + 1] != 'Z') return null;
        var e = at + (long)BitConverter.ToUInt32(data, at + 0x3C);
        if (e + 24 > data.Length || BitConverter.ToUInt32(data, (int)e) != 0x4550) return null;
        var count = BitConverter.ToUInt16(data, (int)e + 6);
        var table = e + 24 + BitConverter.ToUInt16(data, (int)e + 20);
        if (count == 0 || table + count * 40L > data.Length) return null;
        long end = 0;
        for (var i = 0; i < count; i++)
        {
            var entry = (int)(table + i * 40L);
            end = Math.Max(end, at + (long)BitConverter.ToUInt32(data, entry + 20) + BitConverter.ToUInt32(data, entry + 16));
        }
        return (end, (BitConverter.ToUInt16(data, (int)e + 22) & 0x2000) != 0);
    }

    // -- The copy kept on this machine ---------------------------------------------------------------

    private string KeptPath => Path.Combine(AppPaths.Runtimes, OriginalSha256 + ".dll");

    /// <summary>The original this app kept, when it is there and still hashes to the build.</summary>
    public string? Kept() => PayloadCache.Verified(KeptPath, OriginalSize, OriginalSha256) ? KeptPath : null;

    /// <summary>Reads a file somebody pointed at (see <see cref="Read"/>) and keeps the checked original.</summary>
    public static UserRuntime Keep(string path, IReadOnlyList<UserRuntime> builds)
    {
        var (build, original) = Read(path, builds);
        if (build.Kept() is not null) return build;
        var temp = build.KeptPath + ".tmp";
        try
        {
            File.WriteAllBytes(temp, original);
            File.Move(temp, build.KeptPath, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new InstallException($"Could not keep danielblnc's runtime {build.Name} in {AppPaths.Runtimes}: {e.Message}");
        }
        return build;
    }
}
