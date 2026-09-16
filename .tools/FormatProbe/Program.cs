using System.Reflection;
using SIPSorceryMedia.Abstractions;

var t = typeof(VideoFormat);
Console.WriteLine($"VideoFormat: {t.FullName} from {t.Assembly.Location}");

Console.WriteLine("Constructors:");
foreach (var ctor in t.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
{
    Console.WriteLine($"- {ctor}");
}

Console.WriteLine("Properties:");
foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
{
    Console.WriteLine($"- {p.PropertyType.Name} {p.Name}");
}

Console.WriteLine("Fields:");
foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic))
{
    Console.WriteLine($"- {f.FieldType.Name} {f.Name}");
}

Console.WriteLine();
var enc = typeof(IVideoEncoder);
Console.WriteLine($"{enc.FullName}:");
foreach (var m in enc.GetMethods().OrderBy(m => m.Name, StringComparer.Ordinal).ThenBy(m => m.ToString(), StringComparer.Ordinal))
{
    Console.WriteLine($"- {m}");
}
