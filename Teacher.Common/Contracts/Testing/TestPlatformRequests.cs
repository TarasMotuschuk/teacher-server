#pragma warning disable SA1402

namespace Teacher.Common.Contracts.Testing;

public sealed record ResolveStudentRequest(
    string Surname,
    string Name,
    string? Middlename,
    string? ClassName,
    string? DeviceId);

public sealed record CreateAssignmentRequest(
    string TestPublicId,
    int TestVersion,
    string Title,
    AssignmentAudienceDto Audience,
    AssignmentAvailabilityDto Availability,
    AttemptPolicyDto AttemptPolicy,
    ResultPolicyDto ResultPolicy);

public sealed record StartAttemptRequest(
    string AssignmentPublicId,
    AttemptStudentDto Student);

public sealed record ClientProgressDto(
    int AnsweredCount,
    int QuestionCount,
    string? CurrentQuestionId);

public sealed record SaveAttemptProgressRequest(
    IReadOnlyList<AttemptAnswerDto> Answers,
    ClientProgressDto? ClientProgress);

public sealed record SubmitAttemptRequest(
    IReadOnlyList<AttemptAnswerDto> Answers);

#pragma warning restore SA1402
