#pragma warning disable SA1402

namespace Teacher.Common.Contracts.Testing;

public sealed record PagedResponseDto<T>(
    IReadOnlyList<T> Items,
    int Total);

public sealed record TestDefinitionListItemDto(
    string PublicId,
    int Version,
    string Title,
    string? Description,
    string? Grade,
    IReadOnlyList<string> Subjects,
    int QuestionCount,
    DateTime UpdatedAtUtc);

public sealed record ActiveAssignmentDto(
    string AssignmentPublicId,
    string Title,
    DateTime? StartUtc,
    DateTime? EndUtc);

public sealed record ResolveStudentResponse(
    AttemptStudentDto Student,
    IReadOnlyList<ActiveAssignmentDto> ActiveAssignments);

public sealed record StartAttemptResponse(
    string AttemptPublicId,
    AttemptStatus Status,
    DateTime StartedAtUtc,
    DateTime? LastSavedAtUtc,
    string AttemptToken,
    AssignmentDto Assignment,
    TestDefinitionDto TestDefinition,
    IReadOnlyList<AttemptAnswerDto> SavedAnswers);

public sealed record SaveAttemptProgressResponse(
    string AttemptPublicId,
    AttemptStatus Status,
    DateTime LastSavedAtUtc);

public sealed record SubmitAttemptResponse(
    string AttemptPublicId,
    AttemptStatus Status,
    DateTime SubmittedAtUtc,
    ResultDto Result,
    ResultPolicyDto ResultView);

public sealed record MyTestImportResponseDto(
    string ImportId,
    string Status,
    TestDefinitionDto TestDefinition,
    IReadOnlyList<string> Warnings);

public sealed record AttemptListItemDto(
    string AttemptPublicId,
    AttemptStudentDto Student,
    AttemptStatus Status,
    DateTime StartedAtUtc,
    DateTime? LastSavedAtUtc,
    int AnsweredCount,
    int QuestionCount);

#pragma warning restore SA1402
