# ClassCommander Test Platform DTO Draft

## Purpose

This document is the first DTO draft for the ClassCommander testing platform.

It translates the current conceptual work into likely C# transfer objects for:

- `Teacher.Common`
- server API contracts
- import/export flows
- student runtime payloads

It is based on:

- [TestPlatformSchema.md](./TestPlatformSchema.md)
- [TestPlatformApi.md](./TestPlatformApi.md)
- [TestPlatformStorage.md](./TestPlatformStorage.md)

## Scope

This DTO draft covers:

- canonical test definition DTOs
- assignment, attempt, and result DTOs
- core enums
- student runtime request/response DTOs

This draft does not yet cover:

- EF entities
- SQLite migrations
- repository interfaces
- sync DTOs for Drupal or WordPress
- package manifest DTOs for `.cctest`

## Namespace Direction

Recommended namespace direction inside `Teacher.Common`:

- `Teacher.Common.Contracts.Testing`

## DTO Style

Recommended style:

- use `record` types for DTOs
- prefer immutable DTOs
- use enums for stable canonical values where possible
- use strongly typed `Interaction` and `AnswerKey` payloads

## Core Enums

## `QuestionType`

```csharp
public enum QuestionType
{
    SingleChoice,
    MultipleChoice,
    Ordering,
    Matching,
    TrueFalseGroup,
    NumericInputGroup,
    TextInput,
    ImagePoint,
    LetterOrdering
}
```

## `TestStatus`

```csharp
public enum TestStatus
{
    Draft,
    Published,
    Archived
}
```

## `AssignmentStatus`

```csharp
public enum AssignmentStatus
{
    Draft,
    Published,
    Closed,
    Archived
}
```

## `AttemptStatus`

```csharp
public enum AttemptStatus
{
    NotStarted,
    InProgress,
    Submitted,
    Scored,
    Expired,
    Cancelled
}
```

## `AssetKind`

```csharp
public enum AssetKind
{
    Image
}
```

## `AudienceType`

```csharp
public enum AudienceType
{
    Class,
    StudentSelection
}
```

## Canonical Definition DTOs

## `TestDefinitionDto`

```csharp
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
```

## `AuthorDto`

```csharp
public sealed record AuthorDto(
    string Name,
    string? Email);
```

## `TestSourceDto`

```csharp
public sealed record TestSourceDto(
    string Kind,
    string? OriginalFileName,
    DateTime? ImportedAtUtc,
    IReadOnlyDictionary<string, string>? Metadata);
```

## `TestSettingsDto`

```csharp
public sealed record TestSettingsDto(
    bool ShuffleQuestions,
    bool ShuffleOptions,
    bool ShowCorrectAnswersAfterFinish,
    int? TimeLimitSeconds,
    int DefaultAttemptLimit,
    string ResultVisibility);
```

## `TestGroupDto`

```csharp
public sealed record TestGroupDto(
    string Id,
    string Title,
    string? Description,
    int Order,
    IReadOnlyList<QuestionDto> Questions);
```

## `QuestionAssetDto`

```csharp
public sealed record QuestionAssetDto(
    string Id,
    AssetKind Kind,
    string MimeType,
    string Path,
    int? Width,
    int? Height,
    AssetSourceDto? Source);
```

## `AssetSourceDto`

```csharp
public sealed record AssetSourceDto(
    string? OriginalFileName);
```

## `QuestionAssetRefDto`

```csharp
public sealed record QuestionAssetRefDto(
    string AssetId,
    string Role);
```

## `QuestionSourceDto`

```csharp
public sealed record QuestionSourceDto(
    string? MyTestType,
    IReadOnlyList<string>? ImportWarnings);
```

## Question Envelope DTO

For version 1, one practical approach is:

- one `QuestionDto`
- one typed `InteractionDto`
- one typed `AnswerKeyDto`

## `QuestionDto`

```csharp
public sealed record QuestionDto(
    string Id,
    QuestionType Type,
    string Prompt,
    string? Description,
    decimal Score,
    bool Required,
    IReadOnlyList<QuestionAssetRefDto> Assets,
    QuestionContentDto? Content,
    QuestionInteractionDto Interaction,
    QuestionAnswerKeyDto AnswerKey,
    QuestionSourceDto? Source);
```

### Note

The current implementation already uses `System.Text.Json` polymorphic serialization with:

- `QuestionContentDto`
- `QuestionInteractionDto`
- `QuestionAnswerKeyDto`
- `AttemptAnswerValueDto`

## Interaction DTOs

## Shared Support DTOs

```csharp
public sealed record OptionDto(
    string Id,
    string Text,
    int Order);

public sealed record MatchingItemDto(
    string Id,
    string Text,
    int Order);

public sealed record StatementDto(
    string Id,
    string Text,
    int Order);

public sealed record NumericEntryDto(
    string Id,
    string Caption,
    int Order);
```

## `SingleChoiceInteractionDto`

```csharp
public sealed record SingleChoiceInteractionDto(
    IReadOnlyList<OptionDto> Options);
```

## `MultipleChoiceInteractionDto`

```csharp
public sealed record MultipleChoiceInteractionDto(
    IReadOnlyList<OptionDto> Options);
```

## `OrderingInteractionDto`

```csharp
public sealed record OrderingInteractionDto(
    IReadOnlyList<OptionDto> Options);
```

## `MatchingInteractionDto`

```csharp
public sealed record MatchingInteractionDto(
    IReadOnlyList<MatchingItemDto> LeftItems,
    IReadOnlyList<MatchingItemDto> RightItems);
```

## `TrueFalseGroupInteractionDto`

```csharp
public sealed record TrueFalseGroupInteractionDto(
    IReadOnlyList<StatementDto> Statements);
```

## `NumericInputGroupInteractionDto`

```csharp
public sealed record NumericInputGroupInteractionDto(
    IReadOnlyList<NumericEntryDto> Entries);
```

## `TextInputInteractionDto`

```csharp
public sealed record TextInputInteractionDto(
    string? Placeholder,
    int? MaxLength);
```

## `ImagePointInteractionDto`

```csharp
public sealed record ImagePointInteractionDto(
    string SelectionMode);
```

## `LetterOrderingInteractionDto`

```csharp
public sealed record LetterOrderingInteractionDto(
    string Mode);
```

## Answer Key DTOs

## `ChoiceAnswerKeyDto`

```csharp
public sealed record ChoiceAnswerKeyDto(
    IReadOnlyList<string> CorrectOptionIds);
```

## `OrderingAnswerKeyDto`

```csharp
public sealed record OrderingAnswerKeyDto(
    IReadOnlyList<string> CorrectOrder);
```

## `MatchingPairDto`

```csharp
public sealed record MatchingPairDto(
    string LeftId,
    string RightId);
```

## `MatchingAnswerKeyDto`

```csharp
public sealed record MatchingAnswerKeyDto(
    IReadOnlyList<MatchingPairDto> Pairs);
```

## `StatementTruthDto`

```csharp
public sealed record StatementTruthDto(
    string StatementId,
    bool Value);
```

## `TrueFalseGroupAnswerKeyDto`

```csharp
public sealed record TrueFalseGroupAnswerKeyDto(
    IReadOnlyList<StatementTruthDto> StatementTruth);
```

## `AcceptedNumberDto`

```csharp
public sealed record AcceptedNumberDto(
    string EntryId,
    IReadOnlyList<decimal> AcceptedNumbers);
```

## `NumericInputGroupAnswerKeyDto`

```csharp
public sealed record NumericInputGroupAnswerKeyDto(
    IReadOnlyList<AcceptedNumberDto> Values);
```

## `TextInputAnswerKeyDto`

```csharp
public sealed record TextInputAnswerKeyDto(
    IReadOnlyList<string> AcceptedTexts,
    bool CaseSensitive,
    bool TrimWhitespace);
```

## `PolygonRegionDto`

```csharp
public sealed record PolygonRegionDto(
    string Shape,
    IReadOnlyList<PointDto> Points);

public sealed record PointDto(
    int X,
    int Y);
```

## `ImagePointAnswerKeyDto`

```csharp
public sealed record ImagePointAnswerKeyDto(
    IReadOnlyList<PolygonRegionDto> Regions);
```

## `LetterOrderingContentDto`

```csharp
public sealed record LetterOrderingContentDto(
    string SourceWord);
```

## `LetterOrderingAnswerKeyDto`

```csharp
public sealed record LetterOrderingAnswerKeyDto(
    string TargetWord,
    bool CaseSensitive);
```

## Assignment DTOs

## `AssignmentDto`

```csharp
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
```

## `AssignmentAudienceDto`

```csharp
public sealed record AssignmentAudienceDto(
    AudienceType Type,
    string? ClassPublicId,
    IReadOnlyList<string>? StudentPublicIds);
```

## `AssignmentAvailabilityDto`

```csharp
public sealed record AssignmentAvailabilityDto(
    DateTime? StartUtc,
    DateTime? EndUtc);
```

## `AttemptPolicyDto`

```csharp
public sealed record AttemptPolicyDto(
    int MaxAttempts,
    int? TimeLimitSeconds);
```

## `ResultPolicyDto`

```csharp
public sealed record ResultPolicyDto(
    bool ShowScore,
    bool ShowCorrectAnswers,
    bool ShowPerQuestionFeedback);
```

## Attempt And Result DTOs

## `AttemptStudentDto`

```csharp
public sealed record AttemptStudentDto(
    string? StudentPublicId,
    string Surname,
    string Name,
    string? Middlename,
    string? ClassName,
    string? DeviceId);
```

## `AttemptDto`

```csharp
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
```

## `AttemptAnswerDto`

```csharp
public sealed record AttemptAnswerDto(
    string QuestionId,
    QuestionType Type,
    AttemptAnswerValueDto Value,
    bool IsFinal,
    DateTime SavedAtUtc);
```

## `ResultDto`

```csharp
public sealed record ResultDto(
    string AttemptPublicId,
    decimal ScoreEarned,
    decimal ScoreMax,
    decimal Percent,
    decimal? Grade,
    DateTime CompletedAtUtc,
    IReadOnlyList<QuestionResultDto> QuestionResults);
```

## `QuestionResultDto`

```csharp
public sealed record QuestionResultDto(
    string QuestionId,
    bool IsCorrect,
    decimal ScoreEarned,
    decimal ScoreMax);
```

## API Request DTOs

## `ResolveStudentRequest`

```csharp
public sealed record ResolveStudentRequest(
    string Surname,
    string Name,
    string? Middlename,
    string? ClassName,
    string? DeviceId);
```

## `CreateAssignmentRequest`

```csharp
public sealed record CreateAssignmentRequest(
    string TestPublicId,
    int TestVersion,
    string Title,
    AssignmentAudienceDto Audience,
    AssignmentAvailabilityDto Availability,
    AttemptPolicyDto AttemptPolicy,
    ResultPolicyDto ResultPolicy);
```

## `StartAttemptRequest`

```csharp
public sealed record StartAttemptRequest(
    string AssignmentPublicId,
    AttemptStudentDto Student);
```

## `SaveAttemptProgressRequest`

```csharp
public sealed record SaveAttemptProgressRequest(
    IReadOnlyList<AttemptAnswerDto> Answers,
    ClientProgressDto? ClientProgress);
```

## `SubmitAttemptRequest`

```csharp
public sealed record SubmitAttemptRequest(
    IReadOnlyList<AttemptAnswerDto> Answers);
```

## `ClientProgressDto`

```csharp
public sealed record ClientProgressDto(
    int AnsweredCount,
    int QuestionCount,
    string? CurrentQuestionId);
```

## API Response DTOs

## `TestDefinitionListItemDto`

```csharp
public sealed record TestDefinitionListItemDto(
    string PublicId,
    int Version,
    string Title,
    string? Description,
    string? Grade,
    IReadOnlyList<string> Subjects,
    int QuestionCount,
    DateTime UpdatedAtUtc);
```

## `PagedResponseDto<T>`

```csharp
public sealed record PagedResponseDto<T>(
    IReadOnlyList<T> Items,
    int Total);
```

## `ResolveStudentResponse`

```csharp
public sealed record ResolveStudentResponse(
    AttemptStudentDto Student,
    IReadOnlyList<ActiveAssignmentDto> ActiveAssignments);
```

## `ActiveAssignmentDto`

```csharp
public sealed record ActiveAssignmentDto(
    string AssignmentPublicId,
    string Title,
    DateTime? StartUtc,
    DateTime? EndUtc);
```

## `StartAttemptResponse`

```csharp
public sealed record StartAttemptResponse(
    string AttemptPublicId,
    AttemptStatus Status,
    DateTime StartedAtUtc,
    DateTime? LastSavedAtUtc,
    string AttemptToken,
    AssignmentDto Assignment,
    TestDefinitionDto TestDefinition,
    IReadOnlyList<AttemptAnswerDto> SavedAnswers);
```

## `SaveAttemptProgressResponse`

```csharp
public sealed record SaveAttemptProgressResponse(
    string AttemptPublicId,
    AttemptStatus Status,
    DateTime LastSavedAtUtc);
```

## `SubmitAttemptResponse`

```csharp
public sealed record SubmitAttemptResponse(
    string AttemptPublicId,
    AttemptStatus Status,
    DateTime SubmittedAtUtc,
    ResultDto Result,
    ResultPolicyDto ResultView);
```

## Open Design Questions

These points should be resolved before moving from DTO draft to implementation:

1. Whether public IDs should stay `string` or move to dedicated value objects later
2. Whether `ResultVisibility` should remain a string or become an enum
3. Whether separate sync DTOs will later diverge from runtime DTOs
4. Whether compact grouped DTO files should later be split into one-type-per-file after the contracts stabilize

## Recommended Next Step

After this DTO draft, the next concrete implementation step should be:

1. Wire these DTOs into the import path from MyTest XML
2. Add the first server-side test-definition endpoints against these contracts
3. Draft the SQLite entity model separately
4. Introduce `.cctest` manifest/package DTOs as the next contract slice
