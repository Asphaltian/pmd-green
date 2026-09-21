using System.IO.Compression;
using System.Text.Json;
using LibRecomp.Mods;

namespace PMDGreen.UI;

internal static class ModInstaller
{
    private const string ManifestName = "mod.json";
    private const string NewExtension = ".new";
    private const string OldExtension = ".old";

    public static void Install(IReadOnlyList<string> files, Action<PromptRequest> ask)
    {
        var (pending, errors) = Start(files);
        if (errors.Count > 0)
        {
            Cancel(pending);
            ask(new PromptRequest("Error Installing Mods", string.Join('\n', errors)) { CancelText = "OK" });
            return;
        }

        var overwrites = pending.Where(installation => File.Exists(installation.Target)).ToList();
        if (overwrites.Count == 0)
        {
            Finish(pending, ask);
            return;
        }

        ask(new PromptRequest("Overwrite Mods?", string.Join('\n', overwrites.Select(Describe)))
        {
            ConfirmText = "Overwrite",
            CancelText = "Cancel",
            CancelStyle = PromptButton.Danger,
            Confirmed = () => Finish(pending, ask),
            Canceled = () => Cancel(pending),
        });
    }

    private static (List<Installation> Pending, List<string> Errors) Start(IReadOnlyList<string> files)
    {
        var pending = new List<Installation>();
        var errors = new List<string>();
        if (files.Any(file => Path.GetExtension(file) == ".dll" || Path.GetFileName(file) == ManifestName))
        {
            errors.Add("Mods have to be installed as their zip files. Install the zip file itself, without extracting it.");
            return (pending, errors);
        }

        foreach (string file in files)
        {
            ZipArchive zip;
            try
            {
                zip = ZipFile.OpenRead(file);
            }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                errors.Add($"{Path.GetFileName(file)} isn't a zip file or a mod.");
                continue;
            }

            using (zip)
            {
                try
                {
                    if (zip.GetEntry(ManifestName) is not null)
                    {
                        StartSingle(file, zip, pending, errors);
                    }
                    else
                    {
                        StartPackage(file, zip, pending, errors);
                    }
                }
                catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
                {
                    errors.Add($"{Path.GetFileName(file)} couldn't be copied to the mods folder.");
                }
            }
        }

        return (pending, errors);
    }

    private static void StartSingle(string file, ZipArchive zip, List<Installation> pending, List<string> errors)
    {
        string target = Path.Combine(ModManager.Folder, Path.GetFileName(file));
        if (ReadManifest(zip) is not { } manifest)
        {
            errors.Add($"{Path.GetFileName(file)} isn't a mod.");
            return;
        }

        File.Copy(file, target + NewExtension, overwrite: true);
        pending.Add(new Installation(target, manifest));
    }

    private static void StartPackage(string file, ZipArchive package, List<Installation> pending, List<string> errors)
    {
        bool found = false;
        foreach (var entry in package.Entries.Where(entry => Path.GetExtension(entry.Name) == ".zip"))
        {
            string target = Path.Combine(ModManager.Folder, entry.Name);
            entry.ExtractToFile(target + NewExtension, overwrite: true);
            ModManifest? manifest;
            using (var zip = ZipFile.OpenRead(target + NewExtension))
            {
                manifest = ReadManifest(zip);
            }

            if (manifest is null)
            {
                File.Delete(target + NewExtension);
                errors.Add($"{entry.Name} in {Path.GetFileName(file)} isn't a mod.");
                continue;
            }

            pending.Add(new Installation(target, manifest));
            found = true;
        }

        if (!found)
        {
            errors.Add($"There are no mods in {Path.GetFileName(file)}.");
        }
    }

    private static ModManifest? ReadManifest(ZipArchive zip)
    {
        if (zip.GetEntry(ManifestName) is not { } entry)
        {
            return null;
        }

        try
        {
            using var stream = entry.Open();
            using var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            return ModManifest.Parse(bytes.ToArray());
        }
        catch (Exception e) when (e is JsonException or FormatException or InvalidDataException)
        {
            return null;
        }
    }

    private static string Describe(Installation installation)
    {
        var old = ModManager.Mods.FirstOrDefault(mod => mod.FileName == Path.GetFileName(installation.Target))?.Manifest;
        var current = installation.Manifest;
        if (old is null)
        {
            return $"? -> {current.DisplayName} ({current.Version})";
        }

        return old.DisplayName == current.DisplayName
            ? $"{old.DisplayName} ({old.Version} -> {current.Version})"
            : $"{old.DisplayName} ({old.Version}) -> {current.DisplayName} ({current.Version})";
    }

    private static void Cancel(List<Installation> pending)
    {
        foreach (var installation in pending)
        {
            File.Delete(installation.Target + NewExtension);
        }
    }

    private static void Finish(List<Installation> pending, Action<PromptRequest> ask)
    {
        var errors = new List<string>();
        foreach (var installation in pending)
        {
            ReplaceWithNew(installation.Target, errors);
        }

        ModManager.Refresh();
        if (errors.Count > 0)
        {
            ask(new PromptRequest("Error Installing Mods", string.Join('\n', errors)) { CancelText = "OK" });
        }
    }

    private static void ReplaceWithNew(string path, List<string> errors)
    {
        string old = path + OldExtension;
        string replacement = path + NewExtension;
        try
        {
            if (File.Exists(path))
            {
                File.Move(path, old, overwrite: true);
            }

            File.Move(replacement, path);
            File.Delete(old);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            File.Delete(replacement);
            if (File.Exists(old) && !File.Exists(path))
            {
                File.Move(old, path);
            }

            errors.Add($"{Path.GetFileName(path)} couldn't be replaced.");
        }
    }

    private sealed record Installation(string Target, ModManifest Manifest);
}
