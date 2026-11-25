using System.Diagnostics;
using System.Text.Json;

namespace YakuzaMovesetCore;

public sealed class ToolSettings
{
    public string Blender { get; set; } = "blender";
    public string PythonScript { get; set; } = "";
    public string FighterCommander { get; set; } = "";
    public string GmtConverter { get; set; } = "";
    public string GmtGmdAddon { get; set; } = "";
}

public static class ToolService
{
    public static ToolSettings Load(string path) => File.Exists(path)
        ? JsonSerializer.Deserialize<ToolSettings>(File.ReadAllText(path)) ?? new ToolSettings()
        : new ToolSettings();

    public static void Save(string path, ToolSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static async Task<string> RunAsync(string executable, IEnumerable<string> arguments, string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(executable)) throw new InvalidOperationException("Configure the executable path first.");
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = start };
        if (!process.Start()) throw new InvalidOperationException($"Could not start {executable}.");
        Task<string> output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> error = process.StandardError.ReadToEndAsync(cancellationToken);
        try { await process.WaitForExitAsync(cancellationToken); }
        catch (OperationCanceledException) { if (!process.HasExited) process.Kill(true); throw; }
        string result = await output + await error;
        if (process.ExitCode != 0) throw new InvalidOperationException($"{executable} exited with code {process.ExitCode}.\n{result}");
        return result;
    }

    public static Task<string> RunBlenderAsync(ToolSettings settings, string operation, string input, string output,
        CancellationToken cancellationToken = default)
    {
        string script = string.IsNullOrWhiteSpace(settings.PythonScript)
            ? Path.Combine(AppContext.BaseDirectory, "blender", "automation.py") : settings.PythonScript;
        if (!File.Exists(script)) throw new FileNotFoundException("Blender automation script is missing.", script);
        return RunAsync(settings.Blender, ["--background", "--factory-startup", "--python", script, "--", operation, input, output,
            settings.GmtGmdAddon], Path.GetDirectoryName(Path.GetFullPath(input))!, cancellationToken);
    }
}
