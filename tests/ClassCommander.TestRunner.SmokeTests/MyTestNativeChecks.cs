using System.IO.Compression;
using System.Text;
using ClassCommander.Testing.Core.Import;
using ClassCommander.Testing.Core.Packaging;
using Teacher.Common.Contracts.Testing;

namespace ClassCommander.TestRunner.SmokeTests;

internal static class MyTestNativeChecks
{
    public static async Task RunAsync(string root)
    {
        var path = Path.Combine(root, "native.mtf");
        var source = Fixture();
        await File.WriteAllBytesAsync(path, source);
        var converter = new MyTestBatchConverter();
        var result = await converter.ConvertAsync(path, Path.Combine(root, "native-output"));
        Check(result.Succeeded && result.QuestionCount == 1, "Native MTF fixture imports without external tools.");
        var package = await new CctestPackageService().OpenAsync(result.PackagePath!, Path.Combine(root, "native-open"));
        var question = package.Definition.Groups.Single().Questions.Single();
        Check(question.Prompt == "Вкладка Макет?" && question.Score == 3
            && ((ChoiceAnswerKeyDto)question.AnswerKey).CorrectOptionIds.SequenceEqual(["opt_2"]), "Native question text, score and non-first answer key survive.");
        Check((await File.ReadAllBytesAsync(Path.Combine(package.ExtractedDirectory, "imports", "native.mtf"))).SequenceEqual(source), "Native import preserves source bytes.");
        foreach (var (bytes, code) in new[]
        {
            (Fixture("10.2.0.9"), "mtf-unsupported-version"),
            (Fixture(password: "secret"), "mtf-password-protected"),
            (Fixture(trailingByte: true), "mtf-invalid-data"),
            (source[..^10], "mtf-invalid-data"),
            (new byte[] { 1, 2, 3 }, "mtf-protected-or-invalid"),
        })
        {
            await File.WriteAllBytesAsync(path, bytes);
            var failure = await converter.ConvertAsync(path, Path.Combine(root, "native-rejected"));
            Check(!failure.Succeeded && failure.ErrorCode == code, $"Native malformed/protected input: expected {code}, got {failure.ErrorCode}.");
            Check(!Directory.Exists(Path.Combine(root, "native-rejected")), "Rejected MTF does not publish a package.");
        }
    }

    private static byte[] Fixture(string version = "10.2.0.2", string password = "", bool trailingByte = false)
    {
        using var body = new MemoryStream();
        using (var w = new BinaryWriter(body, Encoding.Unicode, leaveOpen: true))
        {
            void S(string text)
            { w.Write(text.Length); w.Write(Encoding.Unicode.GetBytes(text)); }
            S("Native fixture");
            S("Teacher");
            S("");
            S("");
            S("");
            w.Write((byte)0);
            S("");
            w.Write(new byte[2 + 4 + 4 + 4 + 4 + 8 + 12 + 16]);
            S(password);
            S("");
            S("");
            S("");
            w.Write(0);
            w.Write(new byte[17]);
            S("native-fixture");
            w.Write(1);
            w.Write(7);
            S("Тема");
            S("");
            w.Write(1);
            w.Write((byte)0);
            w.Write(new byte[26]);
            w.Write(1);
            w.Write((byte)1);
            w.Write(5);
            S("Вкладка Макет?");
            S("");
            S("");
            S("");
            S("");
            S("");
            S("");
            w.Write(3);
            w.Write(0);
            S("");
            S("");
            w.Write(0);
            S("");
            w.Write(7);
            w.Write((byte)1);
            w.Write(2);
            S("Основне");
            S("Макет");
            w.Write(2);
            w.Write(0);
            w.Write(1);
            w.Write(new byte[5]);
            w.Write(0);
            if (trailingByte)
                w.Write((byte)1);
        }
        using var output = new MemoryStream();
        using (var header = new BinaryWriter(output, Encoding.Unicode, leaveOpen: true))
        {
            header.Write(Encoding.Unicode.GetBytes("MyTestX"));
            header.Write(version.Length);
            header.Write(Encoding.Unicode.GetBytes(version));
        }
        using (var zlib = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            zlib.Write(body.ToArray());
        var bytes = output.ToArray();
        var state = 0x0101;
        for (var i = 0; i < bytes.Length; i++)
        {
            var plain = bytes[i];
            var key = state & 255;
            bytes[i] = (byte)(plain ^ key);
            state = (((plain + key) & 255) * 0x6CA9 + 0xCE1C) & 65535;
        }
        return bytes;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
