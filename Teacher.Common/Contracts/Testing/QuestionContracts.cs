using System.Text.Json.Serialization;

#pragma warning disable SA1402

namespace Teacher.Common.Contracts.Testing;

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

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$contentType")]
[JsonDerivedType(typeof(LetterOrderingContentDto), "letter-ordering")]
public abstract record QuestionContentDto;

public sealed record LetterOrderingContentDto(
    string SourceWord) : QuestionContentDto;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$interactionType")]
[JsonDerivedType(typeof(SingleChoiceInteractionDto), "single-choice")]
[JsonDerivedType(typeof(MultipleChoiceInteractionDto), "multiple-choice")]
[JsonDerivedType(typeof(OrderingInteractionDto), "ordering")]
[JsonDerivedType(typeof(MatchingInteractionDto), "matching")]
[JsonDerivedType(typeof(TrueFalseGroupInteractionDto), "true-false-group")]
[JsonDerivedType(typeof(NumericInputGroupInteractionDto), "numeric-input-group")]
[JsonDerivedType(typeof(TextInputInteractionDto), "text-input")]
[JsonDerivedType(typeof(ImagePointInteractionDto), "image-point")]
[JsonDerivedType(typeof(LetterOrderingInteractionDto), "letter-ordering")]
public abstract record QuestionInteractionDto;

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

public sealed record SingleChoiceInteractionDto(
    IReadOnlyList<OptionDto> Options) : QuestionInteractionDto;

public sealed record MultipleChoiceInteractionDto(
    IReadOnlyList<OptionDto> Options) : QuestionInteractionDto;

public sealed record OrderingInteractionDto(
    IReadOnlyList<OptionDto> Options) : QuestionInteractionDto;

public sealed record MatchingInteractionDto(
    IReadOnlyList<MatchingItemDto> LeftItems,
    IReadOnlyList<MatchingItemDto> RightItems) : QuestionInteractionDto;

public sealed record TrueFalseGroupInteractionDto(
    IReadOnlyList<StatementDto> Statements) : QuestionInteractionDto;

public sealed record NumericInputGroupInteractionDto(
    IReadOnlyList<NumericEntryDto> Entries) : QuestionInteractionDto;

public sealed record TextInputInteractionDto(
    string? Placeholder,
    int? MaxLength) : QuestionInteractionDto;

public sealed record ImagePointInteractionDto(
    string SelectionMode) : QuestionInteractionDto;

public sealed record LetterOrderingInteractionDto(
    string Mode) : QuestionInteractionDto;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$answerKeyType")]
[JsonDerivedType(typeof(ChoiceAnswerKeyDto), "choice")]
[JsonDerivedType(typeof(OrderingAnswerKeyDto), "ordering")]
[JsonDerivedType(typeof(MatchingAnswerKeyDto), "matching")]
[JsonDerivedType(typeof(TrueFalseGroupAnswerKeyDto), "true-false-group")]
[JsonDerivedType(typeof(NumericInputGroupAnswerKeyDto), "numeric-input-group")]
[JsonDerivedType(typeof(TextInputAnswerKeyDto), "text-input")]
[JsonDerivedType(typeof(ImagePointAnswerKeyDto), "image-point")]
[JsonDerivedType(typeof(LetterOrderingAnswerKeyDto), "letter-ordering")]
public abstract record QuestionAnswerKeyDto;

public sealed record ChoiceAnswerKeyDto(
    IReadOnlyList<string> CorrectOptionIds) : QuestionAnswerKeyDto;

public sealed record OrderingAnswerKeyDto(
    IReadOnlyList<string> CorrectOrder) : QuestionAnswerKeyDto;

public sealed record MatchingPairDto(
    string LeftId,
    string RightId);

public sealed record MatchingAnswerKeyDto(
    IReadOnlyList<MatchingPairDto> Pairs) : QuestionAnswerKeyDto;

public sealed record StatementTruthDto(
    string StatementId,
    bool Value);

public sealed record TrueFalseGroupAnswerKeyDto(
    IReadOnlyList<StatementTruthDto> StatementTruth) : QuestionAnswerKeyDto;

public sealed record AcceptedNumberDto(
    string EntryId,
    IReadOnlyList<decimal> AcceptedNumbers);

public sealed record NumericInputGroupAnswerKeyDto(
    IReadOnlyList<AcceptedNumberDto> Values) : QuestionAnswerKeyDto;

public sealed record TextInputAnswerKeyDto(
    IReadOnlyList<string> AcceptedTexts,
    bool CaseSensitive,
    bool TrimWhitespace) : QuestionAnswerKeyDto;

public sealed record PointDto(
    int X,
    int Y);

public sealed record PolygonRegionDto(
    string Shape,
    IReadOnlyList<PointDto> Points);

public sealed record ImagePointAnswerKeyDto(
    IReadOnlyList<PolygonRegionDto> Regions) : QuestionAnswerKeyDto;

public sealed record LetterOrderingAnswerKeyDto(
    string TargetWord,
    bool CaseSensitive) : QuestionAnswerKeyDto;

#pragma warning restore SA1402
