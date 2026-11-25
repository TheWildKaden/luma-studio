using System.Buffers.Binary;
using System.Text;

namespace YakuzaMovesetCore;

public sealed record CfcCommand(uint Offset, string Name, string? Animation, uint? AnimationOffset);
public sealed record CfcMoveset(string Name, string Path, IReadOnlyList<CfcCommand> Commands);

public sealed class CfcFile
{
    private readonly byte[] bytes;
    private readonly Dictionary<uint, string> strings;
    private readonly Dictionary<uint, CfcCommand> editable;
    public IReadOnlyList<CfcMoveset> Movesets { get; }
    public IReadOnlyDictionary<uint, string> Animations { get; }

    private CfcFile(byte[] source, Dictionary<uint, string> labels, List<CfcMoveset> groups, Dictionary<uint, CfcCommand> slots)
    {
        bytes = source;
        strings = labels;
        Movesets = groups;
        editable = slots;
        Animations = labels.Where(label => slots.Values.Any(slot => slot.AnimationOffset == label.Key))
            .ToDictionary(label => label.Key, label => label.Value);
    }

    public static CfcFile Open(string path) => Parse(File.ReadAllBytes(path));

    public static CfcFile Parse(byte[] source)
    {
        if (source.Length < 40 || Encoding.ASCII.GetString(source, 0, 4) != "CFCI" ||
            source[4] != 2 || source[5] != 1 || source[6] != 0 || source[7] != 0)
            throw new InvalidDataException("Expected a CFCI 2.1 fighter command file.");

        uint Read(int offset)
        {
            if (offset < 0 || offset > source.Length - 4) throw new InvalidDataException("CFC pointer is outside the file.");
            return BinaryPrimitives.ReadUInt32BigEndian(source.AsSpan(offset, 4));
        }
        int Checked(uint pointer, int length, int limit)
        {
            if (pointer > int.MaxValue || (long)pointer + length > limit)
                throw new InvalidDataException("CFC record exceeds its section.");
            return (int)pointer;
        }

        int footer = source.Length - 8;
        uint count = Read(footer);
        uint table = Read(footer + 4);
        if (count is 0 or > 10000 || table < 40 || (long)table + count * 4 != footer || table % 4 != 0)
            throw new InvalidDataException("Invalid CFC moveset index.");

        var records = new List<(int Offset, List<int> Commands)>();
        int recordsStart = (int)table;
        int previousEnd = 16;
        for (int i = 0; i < count; i++)
        {
            int group = Checked(Read((int)table + i * 4), 24, (int)table);
            if (group < previousEnd || group % 4 != 0) throw new InvalidDataException("Invalid CFC moveset record.");
            uint commandCount = Read(group + 8);
            uint listPointer = Read(group + 12);
            if (commandCount > 100000 || listPointer < previousEnd || listPointer % 4 != 0 ||
                (long)listPointer + commandCount * 4 > group)
                throw new InvalidDataException("Invalid CFC command list.");
            int list = (int)listPointer;
            var commands = new List<int>();
            int last = previousEnd;
            for (int j = 0; j < commandCount; j++)
            {
                int command = Checked(Read(list + j * 4), 20, list);
                if (command < last || command % 4 != 0) throw new InvalidDataException("Invalid CFC command record.");
                recordsStart = Math.Min(recordsStart, command);
                commands.Add(command);
                last = command + 20;
            }
            recordsStart = Math.Min(recordsStart, list);
            records.Add((group, commands));
            previousEnd = group + 24;
        }
        if (recordsStart <= 16 || recordsStart % 4 != 0) throw new InvalidDataException("Invalid CFC label section.");

        var labels = new Dictionary<uint, string>();
        int cursor = 16;
        while (cursor < recordsStart && source[cursor] != 0xcc)
        {
            int end = Array.IndexOf(source, (byte)0, cursor, recordsStart - cursor);
            if (end < 0) throw new InvalidDataException("Unterminated CFC label.");
            labels[(uint)cursor] = Encoding.UTF8.GetString(source, cursor, end - cursor);
            cursor = end + 1;
        }
        if (recordsStart - cursor > 3 || source.AsSpan(cursor, recordsStart - cursor).IndexOfAnyExcept((byte)0xcc) >= 0)
            throw new InvalidDataException("Invalid CFC label alignment.");

        var groups = new List<CfcMoveset>();
        var editable = new Dictionary<uint, CfcCommand>();
        foreach (var record in records)
        {
            if (!labels.TryGetValue(Read(record.Offset), out string? groupName) ||
                !labels.TryGetValue(Read(record.Offset + 4), out string? path))
                throw new InvalidDataException("Invalid CFC moveset label.");
            var commands = new List<CfcCommand>();
            foreach (int offset in record.Commands)
            {
                uint namePointer = Read(offset);
                if (!labels.TryGetValue(namePointer, out string? name)) name = $"Command 0x{offset:X}";
                uint animationPointer = Read(offset + 8);
                string? animationName = null;
                bool direct = (Read(offset + 4) & 0xffff) == 1 &&
                    labels.TryGetValue(animationPointer, out animationName) && !string.IsNullOrEmpty(animationName);
                var command = new CfcCommand((uint)offset, name, direct ? animationName : null,
                    direct ? animationPointer : null);
                commands.Add(command);
                if (direct) editable[(uint)offset] = command;
            }
            groups.Add(new CfcMoveset(groupName, path, commands));
        }
        return new CfcFile(source.ToArray(), labels, groups, editable);
    }

    public byte[] Export(IEnumerable<MoveEdit> edits)
    {
        byte[] output = bytes.ToArray();
        foreach (var edit in edits)
        {
            if (!editable.TryGetValue(edit.CommandOffset, out var command) ||
                !Movesets.Any(group => group.Name == edit.Moveset && group.Commands.Contains(command)))
                throw new InvalidDataException($"Unknown editable command 0x{edit.CommandOffset:X} in {edit.Moveset}.");
            var matches = strings.Where(pair => pair.Value == edit.Animation && Animations.ContainsKey(pair.Key)).ToList();
            if (matches.Count == 0) throw new InvalidDataException($"Unknown animation {edit.Animation}.");
            BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan((int)edit.CommandOffset + 8, 4), matches[0].Key);
        }
        return output;
    }
}
