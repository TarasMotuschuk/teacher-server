using System.Reflection;
using SIPSorcery.Media;
using SIPSorcery.Net;
using Vortice.MediaFoundation;

var pcType = typeof(RTCPeerConnection);

Console.WriteLine($"{pcType.FullName} from {pcType.Assembly.Location}");
Console.WriteLine();

static void DumpMethods(Type t, Func<MethodInfo, bool> filter)
{
    foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
             .Where(filter)
             .OrderBy(m => m.Name, StringComparer.Ordinal)
             .ThenBy(m => m.ToString(), StringComparer.Ordinal))
    {
        Console.WriteLine($"- {m}");
    }
}

Console.WriteLine("RTCPeerConnection methods containing 'H264':");
DumpMethods(pcType, m => m.Name.Contains("H264", StringComparison.OrdinalIgnoreCase));

Console.WriteLine();
Console.WriteLine("RTCPeerConnection methods containing 'SendVideo' or 'SendH264':");
DumpMethods(pcType, m =>
    m.Name.Contains("SendVideo", StringComparison.OrdinalIgnoreCase) ||
    m.Name.Contains("SendH264", StringComparison.OrdinalIgnoreCase));

Console.WriteLine();
Console.WriteLine("RTCPeerConnection events containing 'Video':");
foreach (var ev in pcType.GetEvents(BindingFlags.Public | BindingFlags.Instance)
         .Where(e => e.Name.Contains("Video", StringComparison.OrdinalIgnoreCase))
         .OrderBy(e => e.Name, StringComparer.Ordinal))
{
    Console.WriteLine($"- {ev.Name}: {ev.EventHandlerType}");
}

Console.WriteLine();
Console.WriteLine("Search nested types containing 'H264Packet':");
foreach (var nt in pcType.Assembly.GetTypes().Where(t => t.FullName?.Contains("H264", StringComparison.OrdinalIgnoreCase) == true)
 .OrderBy(t => t.FullName, StringComparer.Ordinal))
{
    if (nt.FullName?.Contains("H264Packet", StringComparison.OrdinalIgnoreCase) == true)
    {
        Console.WriteLine($"- {nt.FullName}");
    }
}

Console.WriteLine();
var packetiser = typeof(H264Packetiser);
Console.WriteLine($"{packetiser.FullName} methods:");
DumpMethods(packetiser, _ => true);

Console.WriteLine();
Console.WriteLine("H264Packetiser.ParseNals smoke test:");
var parse = typeof(H264Packetiser).GetMethod(
    "ParseNals",
    BindingFlags.Public | BindingFlags.Static,
    binder: null,
    types: [typeof(byte[])],
    modifiers: null);
if (parse is null)
{
    Console.WriteLine("Could not find ParseNals via reflection.");
}
else
{
    // Annex B: 0000 00 01 <nal header> ...
    var annexB = new byte[] { 0x00, 0x00, 0x00, 0x01, 0x67, 0x42, 0x00, 0x0A, 0x00, 0x00, 0x00, 0x01, 0x68, 0xCE, 0x3C, 0x80 };
    var nals = parse.Invoke(null, [annexB]);
    Console.WriteLine($"ParseNals returned: {nals?.GetType().FullName}");

    if (nals is System.Collections.IEnumerable en)
    {
        var i = 0;
        foreach (var item in en)
        {
            i++;
            Console.WriteLine($"- nal #{i}: {item}");
        }
    }
}

Console.WriteLine();
Console.WriteLine("Static fields containing 'H264_SUGGESTED_FORMAT_ID':");
foreach (var t in pcType.Assembly.GetTypes())
{
    FieldInfo? f;
    try
    {
        f = t.GetField("H264_SUGGESTED_FORMAT_ID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
    }
    catch
    {
        continue;
    }

    if (f is null)
    {
        continue;
    }

    var val = f.GetValue(null);
    Console.WriteLine($"- {t.FullName}.{f.Name} = {val} ({val?.GetType().Name})");
}

Console.WriteLine();
Console.WriteLine("VideoTestPatternSource static fields containing 'H264':");
foreach (var f in typeof(VideoTestPatternSource).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
         .Where(f => f.Name.Contains("H264", StringComparison.OrdinalIgnoreCase))
         .OrderBy(f => f.Name, StringComparer.Ordinal))
{
    var val = f.GetValue(null);
    Console.WriteLine($"- {f.Name}: {val} ({val?.GetType().Name})");
}

Console.WriteLine();
Console.WriteLine("VideoTestPatternSource constructors:");
foreach (var c in typeof(VideoTestPatternSource).GetConstructors(BindingFlags.Public | BindingFlags.Instance).OrderBy(c => c.ToString(), StringComparer.Ordinal))
{
    Console.WriteLine($"- {c}");
}

Console.WriteLine();
var mfAsm = typeof(MediaFactory).Assembly;
Console.WriteLine($"Vortice.MediaFoundation assembly: {mfAsm.FullName}");
Console.WriteLine(mfAsm.Location);
Console.WriteLine();

Console.WriteLine("Vortice.MediaFoundation types (filtered: H264/MFT/Transform):");
foreach (var t in mfAsm.GetTypes().OrderBy(t => t.FullName, StringComparer.Ordinal))
{
    var n = t.FullName ?? string.Empty;
    if (!n.Contains("H264", StringComparison.OrdinalIgnoreCase) &&
        !n.Contains("Transform", StringComparison.OrdinalIgnoreCase) &&
        !n.Contains("MFT", StringComparison.OrdinalIgnoreCase))
    {
        continue;
    }

    Console.WriteLine($"- {t.FullName}");
}

Console.WriteLine();
Console.WriteLine("Vortice.MediaFoundation static Guid fields containing 'H264' or 'Decoder':");
foreach (var t in mfAsm.GetTypes())
{
    foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
    {
        if (f.FieldType != typeof(Guid))
        {
            continue;
        }

        if (!f.Name.Contains("H264", StringComparison.OrdinalIgnoreCase) &&
            !f.Name.Contains("Decoder", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        try
        {
            var g = (Guid)f.GetValue(null)!;
            Console.WriteLine($"- {t.FullName}.{f.Name} = {g}");
        }
        catch
        {
        }
    }
}

Console.WriteLine();
Console.WriteLine("MediaFactory public static methods containing 'Transform' or 'MFT':");
foreach (var m in typeof(MediaFactory).GetMethods(BindingFlags.Public | BindingFlags.Static)
         .Where(m => m.Name.Contains("Transform", StringComparison.OrdinalIgnoreCase) ||
                     m.Name.Contains("MFT", StringComparison.OrdinalIgnoreCase) ||
                     m.Name.Contains("Mft", StringComparison.OrdinalIgnoreCase))
         .OrderBy(m => m.Name, StringComparer.Ordinal)
         .ThenBy(m => m.ToString(), StringComparer.Ordinal))
{
    Console.WriteLine($"- {m}");
}

Console.WriteLine();
Console.WriteLine("Vortice.MediaFoundation types containing 'MFT' (name only):");
foreach (var t in mfAsm.GetTypes()
         .Where(t => t.Name.Contains("MFT", StringComparison.OrdinalIgnoreCase))
         .OrderBy(t => t.FullName, StringComparer.Ordinal))
{
    Console.WriteLine($"- {t.FullName}");
}

Console.WriteLine();
Console.WriteLine("MediaFactory methods containing 'Sample' or 'MemoryBuffer':");
foreach (var m in typeof(MediaFactory).GetMethods(BindingFlags.Public | BindingFlags.Static)
         .Where(m => m.Name.Contains("Sample", StringComparison.OrdinalIgnoreCase) ||
                     m.Name.Contains("MemoryBuffer", StringComparison.OrdinalIgnoreCase))
         .OrderBy(m => m.Name, StringComparer.Ordinal)
         .ThenBy(m => m.ToString(), StringComparer.Ordinal))
{
    Console.WriteLine($"- {m}");
}

Console.WriteLine();
Console.WriteLine("IMFTransform public instance methods (first 40):");
foreach (var m in typeof(IMFTransform).GetMethods().OrderBy(m => m.Name, StringComparer.Ordinal).Take(40))
{
    Console.WriteLine($"- {m}");
}

Console.WriteLine();
Console.WriteLine("MediaFactory methods containing 'MediaType':");
foreach (var m in typeof(MediaFactory).GetMethods(BindingFlags.Public | BindingFlags.Static)
         .Where(m => m.Name.Contains("MediaType", StringComparison.OrdinalIgnoreCase))
         .OrderBy(m => m.Name, StringComparer.Ordinal)
         .ThenBy(m => m.ToString(), StringComparer.Ordinal))
{
    Console.WriteLine($"- {m}");
}

Console.WriteLine();
Console.WriteLine("IMFTransform IID:");
Console.WriteLine(typeof(IMFTransform).GUID);

Console.WriteLine();
Console.WriteLine("OutputDataBuffer fields:");
foreach (var f in typeof(OutputDataBuffer).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
         .OrderBy(f => f.Name, StringComparer.Ordinal))
{
    Console.WriteLine($"- {f.FieldType.Name} {f.Name}");
}

Console.WriteLine();
Console.WriteLine("IMFAttributes methods containing 'UInt' or 'Int' (first 40):");
foreach (var m in typeof(IMFAttributes).GetMethods().Where(m => m.Name.Contains("UInt", StringComparison.Ordinal) || m.Name.Contains("Int", StringComparison.Ordinal))
         .OrderBy(m => m.Name, StringComparer.Ordinal)
         .Take(40))
{
    Console.WriteLine($"- {m}");
}

Console.WriteLine();
Console.WriteLine("IMFAttributes methods containing 'Guid' (first 40):");
foreach (var m in typeof(IMFAttributes).GetMethods().Where(m => m.Name.Contains("Guid", StringComparison.OrdinalIgnoreCase))
         .OrderBy(m => m.Name, StringComparer.Ordinal)
         .Take(40))
{
    Console.WriteLine($"- {m}");
}

Console.WriteLine();
Console.WriteLine("MediaTypeAttributeKeys static Guid fields containing 'Frame':");
foreach (var t in mfAsm.GetTypes().Where(t => t.Name == "MediaTypeAttributeKeys"))
{
    foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                 .Where(f => f.FieldType == typeof(Guid) && f.Name.Contains("Frame", StringComparison.OrdinalIgnoreCase)))
    {
        Console.WriteLine($"- {f.Name} = {(Guid)f.GetValue(null)!}");
    }
}

Console.WriteLine();
Console.WriteLine("MediaFactory public static fields (first 80):");
foreach (var f in typeof(MediaFactory).GetFields(BindingFlags.Public | BindingFlags.Static)
         .OrderBy(f => f.Name, StringComparer.Ordinal)
         .Take(80))
{
    object? val;
    try
    {
        val = f.GetValue(null);
    }
    catch
    {
        continue;
    }

    Console.WriteLine($"- {f.FieldType.Name} {f.Name} = {val}");
}

Console.WriteLine();
Console.WriteLine("MediaFactory public static fields containing 'Mft' or 'MFT' or 'Transform':");
foreach (var f in typeof(MediaFactory).GetFields(BindingFlags.Public | BindingFlags.Static)
         .Where(f => f.Name.Contains("Mft", StringComparison.OrdinalIgnoreCase) ||
                     f.Name.Contains("MFT", StringComparison.OrdinalIgnoreCase) ||
                     f.Name.Contains("Transform", StringComparison.OrdinalIgnoreCase))
         .OrderBy(f => f.Name, StringComparer.Ordinal))
{
    object? val;
    try
    {
        val = f.GetValue(null);
    }
    catch
    {
        continue;
    }

    Console.WriteLine($"- {f.FieldType.Name} {f.Name} = {val}");
}

Console.WriteLine();
Console.WriteLine("MediaFactory public static methods containing 'Startup' or 'Shutdown' or 'Version':");
foreach (var m in typeof(MediaFactory).GetMethods(BindingFlags.Public | BindingFlags.Static)
         .Where(m => m.Name.Contains("Startup", StringComparison.OrdinalIgnoreCase) ||
                     m.Name.Contains("Shutdown", StringComparison.OrdinalIgnoreCase) ||
                     m.Name.Equals("Version", StringComparison.OrdinalIgnoreCase))
         .OrderBy(m => m.Name, StringComparer.Ordinal)
         .ThenBy(m => m.ToString(), StringComparer.Ordinal))
{
    Console.WriteLine($"- {m}");
}

Console.WriteLine();
Console.WriteLine("MediaFactory.MFStartup parameter names:");
foreach (var m in typeof(MediaFactory).GetMethods(BindingFlags.Public | BindingFlags.Static)
         .Where(m => m.Name == "MFStartup"))
{
    Console.WriteLine($"- {m}");
    foreach (var p in m.GetParameters())
    {
        Console.WriteLine($" - {p.ParameterType.Name} {p.Name}");
    }
}

Console.WriteLine();
Console.WriteLine("ResultCode static fields (filtered):");
foreach (var f in typeof(ResultCode).GetFields(BindingFlags.Public | BindingFlags.Static)
         .Where(f => f.Name.Contains("Need", StringComparison.OrdinalIgnoreCase) ||
                     f.Name.Contains("Transform", StringComparison.OrdinalIgnoreCase) ||
                     f.Name.Contains("Stream", StringComparison.OrdinalIgnoreCase) ||
                     f.Name.Contains("More", StringComparison.OrdinalIgnoreCase) ||
                     f.Name.Equals("Ok", StringComparison.OrdinalIgnoreCase))
         .OrderBy(f => f.Name, StringComparer.Ordinal))
{
    object? val;
    try
    {
        val = f.GetValue(null);
    }
    catch
    {
        continue;
    }

    Console.WriteLine($"- {f.FieldType.Name} {f.Name} = {val}");
}

Console.WriteLine();
Console.WriteLine("TMessageType enum values:");
foreach (var name in Enum.GetNames<TMessageType>().OrderBy(n => n, StringComparer.Ordinal))
{
    Console.WriteLine($"- {name} = {(int)Enum.Parse<TMessageType>(name)}");
}

Console.WriteLine();
Console.WriteLine("IMFMediaBuffer public instance methods (first 40):");
foreach (var m in typeof(IMFMediaBuffer).GetMethods().OrderBy(m => m.Name, StringComparer.Ordinal).Take(40))
{
    Console.WriteLine($"- {m}");
}

Console.WriteLine();
Console.WriteLine("IMFSample public instance methods containing 'Buffer':");
foreach (var m in typeof(IMFSample).GetMethods()
         .Where(m => m.Name.Contains("Buffer", StringComparison.OrdinalIgnoreCase))
         .OrderBy(m => m.Name, StringComparer.Ordinal)
         .ThenBy(m => m.ToString(), StringComparer.Ordinal))
{
    Console.WriteLine($"- {m}");
}
