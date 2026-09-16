using System.Reflection;
using System.Runtime.InteropServices;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.Encoders;

static int? FindExpectedAbi(Assembly encodersAssembly)
{
    foreach (var t in encodersAssembly.GetTypes())
    {
        var f = t.GetField("VPX_ENCODER_ABI_VERSION", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        if (f?.FieldType == typeof(int))
        {
            return (int)f.GetValue(null)!;
        }
    }

    return null;
}

static IntPtr ResolveVpxmd(string libraryName, Assembly _, DllImportSearchPath? __)
{
    if (!OperatingSystem.IsMacOS() || !string.Equals(libraryName, "vpxmd", StringComparison.OrdinalIgnoreCase))
    {
        return IntPtr.Zero;
    }

    var baseDir = AppContext.BaseDirectory;
    var vpxmd = Path.Combine(baseDir, "vpxmd.dylib");
    var libvpx = Path.Combine(baseDir, "libvpx.dylib");
    var appVpxmd = "/Applications/ClassCommander.app/Contents/MacOS/vpxmd.dylib";

    if (File.Exists(vpxmd)) return NativeLibrary.Load(vpxmd);
    if (File.Exists(libvpx)) return NativeLibrary.Load(libvpx);
    if (File.Exists(appVpxmd)) return NativeLibrary.Load(appVpxmd);

    return IntPtr.Zero;
}

var encAsm = typeof(VpxVideoEncoder).Assembly;
Console.WriteLine($"Encoders assembly: {encAsm.Location}");
Console.WriteLine($"Expected VPX ABI: {FindExpectedAbi(encAsm)?.ToString() ?? "(not found)"}");

NativeLibrary.SetDllImportResolver(encAsm, ResolveVpxmd);

try
{
    var enc = new VpxVideoEncoder();
    var width = 320;
    var height = 240;
    var sample = new byte[width * height * 4];
    var encoded = enc.EncodeVideo(width, height, sample, VideoPixelFormatsEnum.Bgra, VideoCodecsEnum.VP8);
    Console.WriteLine($"Encode succeeded. bytes={encoded?.Length ?? 0}");
}
catch (Exception ex)
{
    Console.WriteLine($"Encode failed: {ex.GetType().Name}: {ex.Message}");
}
