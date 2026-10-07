namespace ClassCommander.Testing.Core.Import;

public sealed class MyTestImportException(string code, string detail = "") : IOException($"{code}: {detail}")
{
    public string Code { get; } = code;

    public string Detail { get; } = detail;
}
