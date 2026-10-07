using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Teacher.Common.Contracts.Testing;

namespace ClassCommander.Testing.Core.Import;

/// <summary>Reads the MyTestX 10.2.0.2 binary layout without external programs.</summary>
public sealed class MyTestMtfImporter
{
    private const int MaxBytes = 256 * 1024 * 1024;
    private static readonly string[] Types = ["", "TYPE_TASK_CHOICE_SINGLE", "TYPE_TASK_CHOICE_MULTIPLE",
        "TYPE_TASK_CHOICE_ORDER", "TYPE_TASK_CHOICE_COLLATION", "TYPE_TASK_CHOICE_TRUE_FALSE",
        "TYPE_TASK_ENTER_NUM", "TYPE_TASK_ENTER_TEXT", "TYPE_TASK_IMAGE_POINT", "TYPE_TASK_WORD"];

    public (TestDefinitionDto Definition, IReadOnlyList<string> Warnings) Import(
        Stream source, string? originalFileName, string workingDirectory, CancellationToken cancellationToken = default)
    {
        try
        {
            var encrypted = ReadBounded(source, cancellationToken);
            // XCrypt feedback algorithm: MIT-licensed format research, see THIRD-PARTY-NOTICES.md.
            var state = 0x0101;
            for (var i = 0; i < encrypted.Length; i++)
            {
                if ((i & 65535) == 0)
                    cancellationToken.ThrowIfCancellationRequested();
                var key = state & 255;
                encrypted[i] ^= (byte)key;
                state = (((encrypted[i] + key) & 255) * 0x6CA9 + 0xCE1C) & 65535;
            }
            using var decrypted = new MemoryStream(encrypted, writable: false);
            using var header = new BinaryReader(decrypted, Encoding.Unicode, leaveOpen: true);
            if (Encoding.Unicode.GetString(header.ReadBytes(14)) != "MyTestX")
                throw new MyTestImportException("mtf-protected-or-invalid");
            var version = ReadString(header, 128);
            if (version != "10.2.0.2")
                throw new MyTestImportException("mtf-unsupported-version", version);
            using var inflate = new ZLibStream(decrypted, CompressionMode.Decompress);
            using var body = new MemoryStream(ReadBounded(inflate, cancellationToken), writable: false);
            using var reader = new BinaryReader(body, Encoding.Unicode);
            var document = ReadDocument(reader, version, cancellationToken);
            if (body.Position != body.Length)
                throw new MyTestImportException("mtf-invalid-data");
            cancellationToken.ThrowIfCancellationRequested();
            var (definition, warnings) = new MyTestXmlImporter().ImportDocument(document, originalFileName, workingDirectory);
            return (definition with { Source = definition.Source! with { Kind = "mytest-mtf" } }, warnings);
        }
        catch (Exception ex) when (ex is EndOfStreamException or InvalidDataException or DecoderFallbackException or OverflowException)
        {
            throw new MyTestImportException("mtf-invalid-data");
        }
    }

    private static XDocument ReadDocument(BinaryReader r, string version, CancellationToken token)
    {
        var options = new XElement("TestOptions", new XElement("Title", ReadString(r)),
            new XElement("Author", ReadString(r)), new XElement("AuthorEmail", ReadString(r)),
            Text("Description", ReadString(r)));
        ReadString(r); // Instructions precede an obsolete flag and notes.
        r.ReadByte();
        ReadString(r);
        options.Add(new XElement("IsOrderTaskRandom", r.ReadByte() != 0), new XElement("IsOrderVariantRandom", r.ReadByte() != 0));
        r.ReadBytesExactly(4 + 4 + 4 + 4 + 8 + 12 + 16);
        var passwords = Enumerable.Range(0, 4).Select(_ => ReadString(r)).ToArray();
        if (passwords.Any(p => p.Length != 0))
            throw new MyTestImportException("mtf-password-protected");
        var grades = Count(r, 1000);
        for (var i = 0; i < grades; i++)
        { r.ReadUInt32(); r.ReadUInt32(); ReadString(r); }
        r.ReadBytesExactly(17);
        options.Add(new XElement("TestUID", ReadString(r)));
        var groups = new Dictionary<uint, XElement>();
        var groupCount = Count(r, 10000);
        for (var i = 0; i < groupCount; i++)
        {
            var id = r.ReadUInt32();
            var group = new XElement("Group", new XElement("Title", ReadString(r)), Text("Description", ReadString(r)), new XElement("Tasks"));
            r.ReadUInt32();
            r.ReadByte();
            if (!groups.TryAdd(id, group))
                throw new MyTestImportException("mtf-invalid-data");
        }
        r.ReadBytesExactly(26); // Theme-limit flag and five reserved integer/boolean pairs.
        var images = new List<(XElement Task, string Name)>();
        var questionCount = Count(r, 100000);
        if (questionCount == 0)
            throw new MyTestImportException("no-questions");
        for (var i = 0; i < questionCount; i++)
        {
            token.ThrowIfCancellationRequested();
            var type = r.ReadByte();
            if (type == 0 || type >= Types.Length)
                throw new MyTestImportException("unsupported-type", type.ToString(CultureInfo.InvariantCulture));
            var formulations = Count(r, 5);
            if (formulations != 5)
                throw new MyTestImportException("mtf-invalid-data");
            var question = ReadString(r);
            for (var f = 1; f < formulations; f++)
                ReadString(r);
            var image = ReadString(r);
            var media = ReadString(r);
            if (media.Length != 0)
                throw new MyTestImportException("mtf-unsupported-content");
            var score = r.ReadUInt32();
            r.ReadUInt32();
            ReadString(r);
            ReadString(r);
            r.ReadUInt32();
            ReadString(r);
            var groupId = r.ReadUInt32();
            r.ReadByte();
            var task = new XElement("Task", new XAttribute("Type", Types[type]), new XAttribute("Score", score), Text("QuestionText", question));
            if (!groups.TryGetValue(groupId, out var group))
                throw new MyTestImportException("mtf-invalid-data");
            group.Element("Tasks")!.Add(task);
            if (image.Length != 0)
                images.Add((task, image));
            switch (type)
            {
                case >= 1 and <= 5:
                    var variants = Strings(r, 10);
                    var keys = Enumerable.Range(0, Count(r, 10)).Select(_ => r.ReadInt32()).ToArray();
                    if (keys.Length != variants.Length)
                        throw new MyTestImportException("invalid-answer-key");
                    var active = variants.Length;
                    while (active > 0 && variants[active - 1].Length == 0 && keys[active - 1] == 0)
                        active--;
                    task.Add(new XElement("Variants", variants.Take(active).Select((v, index) =>
                        Text("VariantText", v, new XAttribute("CorrectAnswer", keys[index])))));
                    if (type == 4)
                    {
                        var right = Strings(r, 10).ToList();
                        while (right.Count > 0 && right[^1].Length == 0)
                            right.RemoveAt(right.Count - 1);
                        task.Add(new XElement("Variants2", right.Select(v => Text("VariantText", v))));
                    }
                    break;
                case 6:
                    var numeric = new XElement("InputNum");
                    var entries = r.ReadByte();
                    if (entries is 0 or > 5)
                        throw new MyTestImportException("invalid-answer-key");
                    for (var j = 0; j < entries; j++)
                    {
                        var min = r.ReadDouble();
                        var max = r.ReadDouble();
                        var caption = ReadString(r);
                        if (!double.IsFinite(min) || min != max)
                            throw new MyTestImportException("invalid-answer-key");
                        numeric.Add(new XElement("Value", new XAttribute("Caption", caption), min.ToString("R", CultureInfo.InvariantCulture)));
                    }
                    r.ReadByte();
                    task.Add(numeric);
                    break;
                case 7:
                    var text = ReadString(r);
                    var caseSensitive = r.ReadByte() != 0;
                    var regex = r.ReadByte() != 0;
                    task.Add(new XElement("InputText", new XAttribute("IsCase", caseSensitive), new XAttribute("IsRegExpr", regex),
                        text.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries).Select(v => new XElement("Value", v))));
                    break;
                case 8:
                    var regions = new XElement("Regions");
                    var regionCount = Count(r, 10000);
                    for (var j = 0; j < regionCount; j++)
                    {
                        var points = Count(r, 100000);
                        if (points < 3)
                            throw new MyTestImportException("invalid-answer-key");
                        var coordinates = Enumerable.Range(0, points).Select(_ => $"{r.ReadInt32()},{r.ReadInt32()}").ToArray();
                        regions.Add(new XElement("Region", string.Join(" ", coordinates)));
                    }
                    task.Add(regions);
                    break;
                case 9:
                    task.Add(new XElement("Word", ReadString(r)));
                    break;
            }
            r.ReadBytesExactly(5); // Reserved integer and boolean.
        }
        var assets = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var assetCount = Count(r, 100000);
        for (var i = 0; i < assetCount; i++)
        {
            token.ThrowIfCancellationRequested();
            var name = ReadString(r);
            var bytes = r.ReadBytesExactly(Count(r, MaxBytes));
            if (!assets.TryAdd(name, bytes))
                throw new MyTestImportException("mtf-invalid-data");
        }
        foreach (var (task, name) in images)
        {
            if (!assets.TryGetValue(name, out var bytes))
                throw new MyTestImportException("mtf-invalid-data");
            task.Add(new XElement("QuestionImage", new XAttribute("FileName", name), Convert.ToBase64String(bytes)));
        }
        return new XDocument(new XElement("MyTest", new XElement("Version", version), options, new XElement("Groups", groups.Values)));
    }

    private static XElement Text(string name, string value, XAttribute? attribute = null) =>
        new(name, attribute, new XElement(value.StartsWith("{\\rtf", StringComparison.Ordinal) ? "RTF" : "PlainText", value));

    private static string[] Strings(BinaryReader r, int max) => Enumerable.Range(0, Count(r, max)).Select(_ => ReadString(r)).ToArray();

    private static int Count(BinaryReader r, int max)
    {
        var count = r.ReadUInt32();
        if (count > max)
            throw new MyTestImportException("mtf-invalid-data");
        return (int)count;
    }

    private static string ReadString(BinaryReader r, int max = 8 * 1024 * 1024)
    {
        var count = Count(r, max);
        return new UnicodeEncoding(false, false, true).GetString(r.ReadBytesExactly(checked(count * 2)));
    }

    private static byte[] ReadBounded(Stream stream, CancellationToken token)
    {
        using var output = new MemoryStream();
        var buffer = new byte[65536];
        int count;
        while ((count = stream.Read(buffer)) != 0)
        {
            token.ThrowIfCancellationRequested();
            if (output.Length + count > MaxBytes)
                throw new MyTestImportException("mtf-invalid-data");
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }
}

internal static class MyTestBinaryReaderExtensions
{
    public static byte[] ReadBytesExactly(this BinaryReader reader, int count)
    {
        if (count > reader.BaseStream.Length - reader.BaseStream.Position)
            throw new EndOfStreamException();
        var bytes = reader.ReadBytes(count);
        if (bytes.Length != count)
            throw new EndOfStreamException();
        return bytes;
    }
}
