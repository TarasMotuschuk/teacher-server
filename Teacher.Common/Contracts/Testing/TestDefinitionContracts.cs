#pragma warning disable SA1402

namespace Teacher.Common.Contracts.Testing;

public sealed record TestDefinitionDto(
    int SchemaVersion,
    string Type,
    string PublicId,
    int Version,
    string Title,
    string? Description,
    string Language,
    string? Grade,
    IReadOnlyList<string> Subjects,
    IReadOnlyList<string> Tags,
    AuthorDto? Author,
    TestSourceDto? Source,
    TestSettingsDto Settings,
    IReadOnlyList<QuestionAssetDto> Assets,
    IReadOnlyList<TestGroupDto> Groups);

public sealed record AuthorDto(
    string Name,
    string? Email);

public sealed record TestSourceDto(
    string Kind,
    string? OriginalFileName,
    DateTime? ImportedAtUtc,
    IReadOnlyDictionary<string, string>? Metadata);

public sealed record TestSettingsDto(
    bool ShuffleQuestions,
    bool ShuffleOptions,
    bool ShowCorrectAnswersAfterFinish,
    int? TimeLimitSeconds,
    int DefaultAttemptLimit,
    string ResultVisibility);

public sealed record TestGroupDto(
    string Id,
    string Title,
    string? Description,
    int Order,
    IReadOnlyList<QuestionDto> Questions);

public sealed record QuestionAssetDto(
    string Id,
    AssetKind Kind,
    string MimeType,
    string Path,
    int? Width,
    int? Height,
    AssetSourceDto? Source);

public sealed record AssetSourceDto(
    string? OriginalFileName);

public sealed record QuestionAssetRefDto(
    string AssetId,
    string Role);

public sealed record QuestionSourceDto(
    string? MyTestType,
    IReadOnlyList<string>? ImportWarnings);

#pragma warning restore SA1402
