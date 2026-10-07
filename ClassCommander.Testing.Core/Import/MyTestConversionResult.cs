namespace ClassCommander.Testing.Core.Import;

public sealed record MyTestConversionResult(
    string SourcePath, string? PackagePath, int QuestionCount, IReadOnlyList<string> Warnings,
    string? ErrorCode, string? Detail)
{
    public bool Succeeded => PackagePath is not null;
}
