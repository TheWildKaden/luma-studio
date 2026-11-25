using System.Text.Json.Serialization;

namespace YakuzaMovesetCore;

public sealed class ModProject
{
    public int SchemaVersion { get; set; } = 1;
    public string Name { get; set; } = "New moveset";
    public string BaseCfcPath { get; set; } = "";
    public List<AnimationAsset> Animations { get; set; } = [];
    public List<MoveEdit> Moves { get; set; } = [];
    public List<ComboEdge> Combos { get; set; } = [];
    public List<HeatActionReference> HeatActions { get; set; } = [];
    [JsonExtensionData] public Dictionary<string, System.Text.Json.JsonElement>? OtherData { get; set; }
}

public sealed class MoveEdit
{
    public string Moveset { get; set; } = "";
    public uint CommandOffset { get; set; }
    public string CommandName { get; set; } = "";
    public string Animation { get; set; } = "";
    public string? Notes { get; set; }
    [JsonExtensionData] public Dictionary<string, System.Text.Json.JsonElement>? OtherData { get; set; }
}

public sealed class ComboEdge
{
    public string Moveset { get; set; } = "";
    public uint FromCommandOffset { get; set; }
    public uint ToCommandOffset { get; set; }
    public string Label { get; set; } = "Follow-up";
}

public sealed class AnimationAsset
{
    public string Name { get; set; } = "";
    public string SourcePath { get; set; } = "";
    public string? ExportPath { get; set; }
}

public sealed class HeatActionReference
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string Notes { get; set; } = "";
}
