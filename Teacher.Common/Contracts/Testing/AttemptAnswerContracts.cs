using System.Text.Json.Serialization;

#pragma warning disable SA1402

namespace Teacher.Common.Contracts.Testing;

public sealed record AttemptAnswerDto(
    string QuestionId,
    QuestionType Type,
    AttemptAnswerValueDto Value,
    bool IsFinal,
    DateTime SavedAtUtc);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$answerType")]
[JsonDerivedType(typeof(SingleChoiceAnswerValueDto), "single-choice")]
[JsonDerivedType(typeof(MultipleChoiceAnswerValueDto), "multiple-choice")]
[JsonDerivedType(typeof(OrderingAnswerValueDto), "ordering")]
[JsonDerivedType(typeof(MatchingAnswerValueDto), "matching")]
[JsonDerivedType(typeof(TrueFalseGroupAnswerValueDto), "true-false-group")]
[JsonDerivedType(typeof(NumericInputGroupAnswerValueDto), "numeric-input-group")]
[JsonDerivedType(typeof(TextInputAnswerValueDto), "text-input")]
[JsonDerivedType(typeof(ImagePointAnswerValueDto), "image-point")]
[JsonDerivedType(typeof(LetterOrderingAnswerValueDto), "letter-ordering")]
public abstract record AttemptAnswerValueDto;

public sealed record SingleChoiceAnswerValueDto(
    IReadOnlyList<string> SelectedOptionIds) : AttemptAnswerValueDto;

public sealed record MultipleChoiceAnswerValueDto(
    IReadOnlyList<string> SelectedOptionIds) : AttemptAnswerValueDto;

public sealed record OrderingAnswerValueDto(
    IReadOnlyList<string> OrderedOptionIds) : AttemptAnswerValueDto;

public sealed record MatchingAnswerValuePairDto(
    string LeftId,
    string RightId);

public sealed record MatchingAnswerValueDto(
    IReadOnlyList<MatchingAnswerValuePairDto> Pairs) : AttemptAnswerValueDto;

public sealed record StatementTruthAnswerValueDto(
    string StatementId,
    bool Value);

public sealed record TrueFalseGroupAnswerValueDto(
    IReadOnlyList<StatementTruthAnswerValueDto> StatementTruth) : AttemptAnswerValueDto;

public sealed record NumericEntryAnswerValueDto(
    string EntryId,
    decimal Value);

public sealed record NumericInputGroupAnswerValueDto(
    IReadOnlyList<NumericEntryAnswerValueDto> Entries) : AttemptAnswerValueDto;

public sealed record TextInputAnswerValueDto(
    string Text) : AttemptAnswerValueDto;

public sealed record ImagePointAnswerValueDto(
    int X,
    int Y) : AttemptAnswerValueDto;

public sealed record LetterOrderingAnswerValueDto(
    string Text) : AttemptAnswerValueDto;

#pragma warning restore SA1402
