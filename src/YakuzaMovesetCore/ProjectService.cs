using System.IO.Compression;
using System.Text.Json;

namespace YakuzaMovesetCore;

public static class ProjectService
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public static ModProject Load(string path) => JsonSerializer.Deserialize<ModProject>(File.ReadAllText(path), Options)
        ?? throw new InvalidDataException("Project JSON is empty.");

    public static void Save(string path, ModProject project)
    {
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        string temporary = fullPath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(project, Options));
        File.Move(temporary, fullPath, true);
    }

    public static void Export(ModProject project, string projectPath, string zipPath)
    {
        string source = Resolve(projectPath, project.BaseCfcPath);
        CfcFile cfc = CfcFile.Open(source);
        byte[] replacement = cfc.Export(project.Moves);
        string fullZip = Path.GetFullPath(zipPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullZip)!);
        string temporary = fullZip + ".tmp";
        if (File.Exists(temporary)) File.Delete(temporary);
        try
        {
            using (var zip = ZipFile.Open(temporary, ZipArchiveMode.Create))
            {
                Write(zip, "mod/fighter_command_main.cfc", replacement);
                Write(zip, "backup/original_fighter_command_main.cfc", File.ReadAllBytes(source));
                Write(zip, "mod/project.json", JsonSerializer.SerializeToUtf8Bytes(project, Options));
                foreach (var asset in project.Animations)
                {
                    if (string.IsNullOrWhiteSpace(asset.ExportPath)) continue;
                    string output = Resolve(projectPath, asset.ExportPath);
                    if (!File.Exists(output)) throw new FileNotFoundException("Exported animation is missing.", output);
                    if (Path.GetExtension(output).ToLowerInvariant() != ".gmt")
                        throw new InvalidDataException("Only GMT animation assets can be packaged as game animations.");
                    Write(zip, "mod/animations/" + Path.GetFileName(output), File.ReadAllBytes(output));
                }
                Write(zip, "INSTALL.txt", System.Text.Encoding.UTF8.GetBytes(
                    "Back up your original fighter_command_main.cfc from the installed game.\n" +
                    "Copy mod/fighter_command_main.cfc into the corresponding extracted game data location using your existing archive tooling.\n" +
                    "Place any GMT animation files into their corresponding game asset paths after checking the target skeleton and archive mapping.\n" +
                    "Keep backup/original_fighter_command_main.cfc for restoration.\n" +
                    "This package does not modify game archives automatically. Test on a copy of your game files.\n"));
            }
            File.Move(temporary, fullZip, true);
        }
        catch
        {
            if (File.Exists(temporary)) File.Delete(temporary);
            throw;
        }
    }

    public static string Resolve(string projectPath, string value) => Path.GetFullPath(
        Path.IsPathRooted(value) ? value : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(projectPath))!, value));

    private static void Write(ZipArchive zip, string path, byte[] data)
    {
        using Stream stream = zip.CreateEntry(path, CompressionLevel.Optimal).Open();
        stream.Write(data);
    }
}
