using System.Reflection;
using H264Sharp;
using SIPSorceryMedia.Encoders;

var encodersAsm = typeof(VpxVideoEncoder).Assembly;
Console.WriteLine($"Encoders assembly: {encodersAsm.FullName}");
Console.WriteLine(encodersAsm.Location);
Console.WriteLine();

static bool LooksLikeH264(string name) =>
    name.Contains("H264", StringComparison.OrdinalIgnoreCase) ||
    name.Contains("H26", StringComparison.OrdinalIgnoreCase) ||
    name.Contains("AVC", StringComparison.OrdinalIgnoreCase);

Console.WriteLine("Types (filtered):");
foreach (var t in encodersAsm.GetTypes().OrderBy(t => t.FullName, StringComparer.Ordinal))
{
    if (!LooksLikeH264(t.Name))
    {
        continue;
    }

    Console.WriteLine($"- {t.FullName}");
}

Console.WriteLine();
Console.WriteLine("VpxVideoEncoder public methods (for comparison):");
foreach (var m in typeof(VpxVideoEncoder).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
 .OrderBy(m => m.Name, StringComparer.Ordinal))
{
    Console.WriteLine($"- {m}");
}

Console.WriteLine();
Console.WriteLine("VpxVideoEncoder.SupportedFormats:");
using var vpx = new VpxVideoEncoder();
foreach (var f in vpx.SupportedFormats.OrderBy(f => f.Codec).ThenBy(f => f.FormatID))
{
    Console.WriteLine($"- codec={f.Codec}, formatId={f.FormatID}, clock={f.ClockRate}, name={f.FormatName}, parameters={f.Parameters}");
}

Console.WriteLine();
var h264Asm = typeof(H264Encoder).Assembly;
Console.WriteLine($"H264Sharp assembly: {h264Asm.FullName}");
Console.WriteLine(h264Asm.Location);
Console.WriteLine();

Console.WriteLine("H264Sharp types (filtered):");
foreach (var t in h264Asm.GetTypes().OrderBy(t => t.FullName, StringComparer.Ordinal))
{
    if (t.FullName?.Contains("H264", StringComparison.OrdinalIgnoreCase) != true)
    {
        continue;
    }

    Console.WriteLine($"- {t.FullName}");
}

Console.WriteLine();
Console.WriteLine("H264Encoder public instance methods:");
foreach (var m in typeof(H264Encoder).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
         .OrderBy(m => m.Name, StringComparer.Ordinal)
         .ThenBy(m => m.ToString(), StringComparer.Ordinal))
{
    Console.WriteLine($"- {m}");
}

Console.WriteLine();
Console.WriteLine("H264Decoder public instance methods:");
foreach (var m in typeof(H264Decoder).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
         .OrderBy(m => m.Name, StringComparer.Ordinal)
         .ThenBy(m => m.ToString(), StringComparer.Ordinal))
{
    Console.WriteLine($"- {m}");
}
