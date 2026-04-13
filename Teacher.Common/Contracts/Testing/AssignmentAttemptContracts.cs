#pragma warning disable SA1402

namespace Teacher.Common.Contracts.Testing;

public sealed record AssignmentDto(
    string PublicId,
    string TestPublicId,
    int TestVersion,
    string Title,
    AssignmentAudienceDto Audience,
    AssignmentAvailabilityDto Availability,
    AttemptPolicyDto AttemptPolicy,
    ResultPolicyDto ResultPolicy,
    AssignmentStatus Status);

public sealed record AssignmentAudienceDto(
    AudienceType Type,
    string? ClassPublicId,
    IReadOnlyList<string>? StudentPublicIds);

public sealed record AssignmentAvailabilityDto(
    DateTime? StartUtc,
    DateTime? EndUtc);

public sealed record AttemptPolicyDto(
    int MaxAttempts,
    int? TimeLimitSeconds);

public sealed record ResultPolicyDto(
    bool ShowScore,
    bool ShowCorrectAnswers,
    bool ShowPerQuestionFeedback);

public sealed record AttemptStudentDto(
    string? StudentPublicId,
    string Surname,
    string Name,
    string? Middlename,
    string? ClassName,
    string? DeviceId);

public sealed record AttemptDto(
    string PublicId,
    string AssignmentPublicId,
    string TestPublicId,
    int TestVersion,
    AttemptStudentDto Student,
    AttemptStatus Status,
    DateTime StartedAtUtc,
    DateTime? LastSavedAtUtc,
    DateTime? SubmittedAtUtc,
    IReadOnlyList<AttemptAnswerDto> Answers);

public sealed record ResultDto(
    string AttemptPublicId,
    decimal ScoreEarned,
    decimal ScoreMax,
    decimal Percent,
    decimal? Grade,
    DateTime CompletedAtUtc,
    IReadOnlyList<QuestionResultDto> QuestionResults);

public sealed record QuestionResultDto(
    string QuestionId,
    bool IsCorrect,
    decimal ScoreEarned,
    decimal ScoreMax);

#pragma warning restore SA1402
