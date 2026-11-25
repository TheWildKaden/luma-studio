using Microsoft.Win32;
using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using YakuzaMovesetCore;

namespace YakuzaMovesetCreator;

public sealed class RelayCommand(Action action, Func<bool>? enabled = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => enabled?.Invoke() ?? true;
    public void Execute(object? parameter) => action();
    public void Refresh() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class MainViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private ModProject project = new();
    private CfcFile? cfc;
    private string projectPath = "";
    private readonly Stack<string> undo = new();
    private readonly Stack<string> redo = new();
    private string status = "Create or open a project, then load a CFC file.";
    private CfcMoveset? selectedMoveset;
    private CfcCommand? selectedCommand;
    private string? selectedAnimation;

    public ToolSettings Settings { get; } = ToolService.Load(SettingsPath);
    private static string SettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Yakuza0MovesetCreator", "tools.json");
    public ObservableCollection<CfcMoveset> Movesets { get; } = [];
    public ObservableCollection<CfcCommand> Commands { get; } = [];
    public ObservableCollection<string> AnimationNames { get; } = [];
    public ObservableCollection<HeatActionReference> HeatActions { get; } = [];
    public string Status { get => status; set { status = value; Changed(); } }
    public CfcMoveset? SelectedMoveset
    {
        get => selectedMoveset;
        set
        {
            selectedMoveset = value;
            Changed();
            Commands.Clear();
            if (value != null) foreach (var command in value.Commands.Where(command => command.AnimationOffset.HasValue)) Commands.Add(command);
            SelectedCommand = Commands.FirstOrDefault();
            DrawGraph?.Invoke();
        }
    }
    public CfcCommand? SelectedCommand
    {
        get => selectedCommand;
        set
        {
            selectedCommand = value;
            Changed();
            SelectedAnimation = value == null ? null : project.Moves.FirstOrDefault(edit => edit.CommandOffset == value.Offset)?.Animation ?? value.Animation;
        }
    }
    public string? SelectedAnimation { get => selectedAnimation; set { selectedAnimation = value; Changed(); } }
    public string EdgeFrom { get; set; } = "";
    public string EdgeTo { get; set; } = "";
    public string EdgeLabel { get; set; } = "Follow-up";
    public string AnimationInput { get; set; } = "";
    public string AnimationOutput { get; set; } = "";
    public string HeatName { get; set; } = "";
    public string HeatPath { get; set; } = "";
    public Action? DrawGraph { get; set; }
    public IReadOnlyList<ComboEdge> VisibleEdges => project.Combos.Where(edge => edge.Moveset == SelectedMoveset?.Name).ToList();

    public ICommand NewCommand { get; }
    public ICommand OpenCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand LoadCfcCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand UndoCommand { get; }
    public ICommand RedoCommand { get; }
    public ICommand ApplyMoveCommand { get; }
    public ICommand AddEdgeCommand { get; }
    public ICommand AddHeatCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand CheckBlenderCommand { get; }
    public ICommand InspectAnimationCommand { get; }
    public ICommand ExportGlbCommand { get; }
    public ICommand SaveBlendCommand { get; }

    public MainViewModel()
    {
        NewCommand = Safe(() => { project = new ModProject(); projectPath = ""; ResetProject(); Status = "New project created."; });
        OpenCommand = Safe(() => { var dialog = new OpenFileDialog { Filter = "Moveset project|*.json" }; if (dialog.ShowDialog() == true) { project = ProjectService.Load(dialog.FileName); projectPath = dialog.FileName; ResetProject(); Status = $"Opened {project.Name}."; } });
        SaveCommand = Safe(Save);
        LoadCfcCommand = Safe(() => { var dialog = new OpenFileDialog { Filter = "Fighter command|*.cfc" }; if (dialog.ShowDialog() == true) LoadCfc(dialog.FileName); });
        ExportCommand = Safe(() => { if (cfc == null) throw new InvalidOperationException("Load a CFC first."); Save(); if (string.IsNullOrEmpty(projectPath)) return; var dialog = new SaveFileDialog { Filter = "Mod package|*.zip", FileName = "Yakuza0MovesetMod.zip" }; if (dialog.ShowDialog() == true) { ProjectService.Export(project, projectPath, dialog.FileName); Status = $"Exported {dialog.FileName}."; } });
        UndoCommand = Safe(() => Restore(undo, redo));
        RedoCommand = Safe(() => Restore(redo, undo));
        ApplyMoveCommand = Safe(ApplyMove);
        AddEdgeCommand = Safe(AddEdge);
        AddHeatCommand = Safe(() => { if (string.IsNullOrWhiteSpace(HeatName)) throw new InvalidOperationException("Enter a heat action name."); Snapshot(); var item = new HeatActionReference { Name = HeatName.Trim(), Path = HeatPath.Trim() }; project.HeatActions.Add(item); HeatActions.Add(item); Status = $"Added heat action reference {item.Name}."; });
        SaveSettingsCommand = Safe(() => { ToolService.Save(SettingsPath, Settings); Status = "Tool paths saved."; });
        CheckBlenderCommand = Safe(async () => { Status = await ToolService.RunAsync(Settings.Blender, ["--version"], Environment.CurrentDirectory); });
        InspectAnimationCommand = Safe(async () => await Blender("inspect"));
        ExportGlbCommand = Safe(async () => await Blender("export_glb"));
        SaveBlendCommand = Safe(async () => await Blender("save_blend"));
    }

    private ICommand Safe(Action action) => new RelayCommand(() => { try { action(); } catch (Exception ex) { Report(ex); } });
    private ICommand Safe(Func<Task> action) => new RelayCommand(async () => { try { await action(); } catch (Exception ex) { Report(ex); } });
    private void Report(Exception error) { Status = error.Message; MessageBox.Show(error.Message, "Yakuza 0 Moveset Creator", MessageBoxButton.OK, MessageBoxImage.Error); }
    private void Snapshot() { undo.Push(System.Text.Json.JsonSerializer.Serialize(project)); redo.Clear(); }
    private void Restore(Stack<string> from, Stack<string> to)
    {
        if (from.Count == 0) return;
        to.Push(System.Text.Json.JsonSerializer.Serialize(project));
        project = System.Text.Json.JsonSerializer.Deserialize<ModProject>(from.Pop())!;
        var group = SelectedMoveset?.Name;
        ResetProject(false);
        SelectedMoveset = Movesets.FirstOrDefault(item => item.Name == group);
        Status = "Project change restored.";
    }
    private void ResetProject(bool clearHistory = true)
    {
        if (clearHistory) { undo.Clear(); redo.Clear(); }
        HeatActions.Clear(); foreach (var item in project.HeatActions) HeatActions.Add(item);
        Movesets.Clear(); Commands.Clear(); AnimationNames.Clear(); cfc = null;
        SelectedMoveset = null;
        if (!string.IsNullOrWhiteSpace(project.BaseCfcPath) && !string.IsNullOrWhiteSpace(projectPath))
        {
            string path = ProjectService.Resolve(projectPath, project.BaseCfcPath);
            if (File.Exists(path)) LoadCfc(path);
        }
    }
    private void LoadCfc(string path)
    {
        var loaded = CfcFile.Open(path);
        cfc = loaded;
        project.BaseCfcPath = path;
        Movesets.Clear(); foreach (var group in loaded.Movesets.OrderBy(group => group.Name)) Movesets.Add(group);
        AnimationNames.Clear(); foreach (var name in loaded.Animations.Values.Distinct().Order()) AnimationNames.Add(name);
        SelectedMoveset = Movesets.FirstOrDefault(group => group.Name == "p_kiryu_c") ?? Movesets.FirstOrDefault();
        Status = $"Loaded {Movesets.Count} movesets from {Path.GetFileName(path)}.";
    }
    private void Save()
    {
        if (string.IsNullOrEmpty(projectPath))
        {
            var dialog = new SaveFileDialog { Filter = "Moveset project|*.json", FileName = "moveset-project.json" };
            if (dialog.ShowDialog() != true) return;
            projectPath = dialog.FileName;
        }
        ProjectService.Save(projectPath, project);
        Status = $"Saved {projectPath}.";
    }
    private void ApplyMove()
    {
        if (SelectedMoveset == null || SelectedCommand == null || SelectedAnimation == null || cfc == null ||
            !cfc.Animations.Values.Contains(SelectedAnimation)) throw new InvalidOperationException("Select an existing animation ID for an editable command.");
        Snapshot();
        project.Moves.RemoveAll(edit => edit.CommandOffset == SelectedCommand.Offset);
        if (SelectedAnimation != SelectedCommand.Animation) project.Moves.Add(new MoveEdit { Moveset = SelectedMoveset.Name,
            CommandOffset = SelectedCommand.Offset, CommandName = SelectedCommand.Name, Animation = SelectedAnimation });
        Status = $"Mapped {SelectedCommand.Name} to {SelectedAnimation}.";
    }
    private void AddEdge()
    {
        if (SelectedMoveset == null || !uint.TryParse(EdgeFrom, out uint from) || !uint.TryParse(EdgeTo, out uint to) ||
            !SelectedMoveset.Commands.Any(command => command.Offset == from) || !SelectedMoveset.Commands.Any(command => command.Offset == to))
            throw new InvalidOperationException("Enter two command offsets from the selected moveset.");
        Snapshot(); project.Combos.Add(new ComboEdge { Moveset = SelectedMoveset.Name, FromCommandOffset = from, ToCommandOffset = to, Label = EdgeLabel });
        DrawGraph?.Invoke(); Status = "Combo branch added to the project graph.";
    }
    private async Task Blender(string operation)
    {
        if (string.IsNullOrWhiteSpace(AnimationInput) || string.IsNullOrWhiteSpace(AnimationOutput))
            throw new InvalidOperationException("Enter an input and output file.");
        Status = "Running Blender...";
        Status = await ToolService.RunBlenderAsync(Settings, operation, AnimationInput, AnimationOutput);
    }
}
