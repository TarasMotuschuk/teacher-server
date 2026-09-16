using Teacher.Common.Contracts.Testing;

namespace ClassCommander.TestEditor.Services;

internal static class QuestionFactory
{
    public static TestGroupDto CreateDefaultGroup(int order = 1)
        => new(
            Id: $"group_{Guid.NewGuid():N}"[..14],
            Title: "1",
            Description: null,
            Order: order,
            Questions: []);

    public static QuestionDto Create(QuestionType type, int orderHint = 1)
    {
        var id = $"q_{Guid.NewGuid():N}"[..14];
        var (interaction, answerKey, content) = CreatePayload(type);
        return new QuestionDto(
            Id: id,
            Type: type,
            Prompt: string.Empty,
            Description: null,
            Score: 1,
            Required: true,
            Assets: [],
            Content: content,
            Interaction: interaction,
            AnswerKey: answerKey,
            Source: null);
    }

    private static (QuestionInteractionDto Interaction, QuestionAnswerKeyDto AnswerKey, QuestionContentDto? Content) CreatePayload(
        QuestionType type)
    {
        return type switch
        {
            QuestionType.SingleChoice => (
                new SingleChoiceInteractionDto(
                [
                    new OptionDto("opt_a", "A", 1),
                    new OptionDto("opt_b", "B", 2),
                ]),
                new ChoiceAnswerKeyDto(["opt_a"]),
                null),
            QuestionType.MultipleChoice => (
                new MultipleChoiceInteractionDto(
                [
                    new OptionDto("opt_a", "A", 1),
                    new OptionDto("opt_b", "B", 2),
                    new OptionDto("opt_c", "C", 3),
                ]),
                new ChoiceAnswerKeyDto(["opt_a"]),
                null),
            QuestionType.Ordering => (
                new OrderingInteractionDto(
                [
                    new OptionDto("opt_1", "1", 1),
                    new OptionDto("opt_2", "2", 2),
                    new OptionDto("opt_3", "3", 3),
                ]),
                new OrderingAnswerKeyDto(["opt_1", "opt_2", "opt_3"]),
                null),
            QuestionType.Matching => (
                new MatchingInteractionDto(
                [
                    new MatchingItemDto("left_1", "Left 1", 1),
                    new MatchingItemDto("left_2", "Left 2", 2),
                ],
                [
                    new MatchingItemDto("right_1", "Right 1", 1),
                    new MatchingItemDto("right_2", "Right 2", 2),
                ]),
                new MatchingAnswerKeyDto(
                [
                    new MatchingPairDto("left_1", "right_1"),
                    new MatchingPairDto("left_2", "right_2"),
                ]),
                null),
            QuestionType.TrueFalseGroup => (
                new TrueFalseGroupInteractionDto(
                [
                    new StatementDto("st_1", "Statement 1", 1),
                    new StatementDto("st_2", "Statement 2", 2),
                ]),
                new TrueFalseGroupAnswerKeyDto(
                [
                    new StatementTruthDto("st_1", true),
                    new StatementTruthDto("st_2", false),
                ]),
                null),
            QuestionType.NumericInputGroup => (
                new NumericInputGroupInteractionDto(
                [
                    new NumericEntryDto("num_1", "Answer", 1),
                ]),
                new NumericInputGroupAnswerKeyDto(
                [
                    new AcceptedNumberDto("num_1", [0]),
                ]),
                null),
            QuestionType.TextInput => (
                new TextInputInteractionDto(Placeholder: null, MaxLength: null),
                new TextInputAnswerKeyDto([string.Empty], CaseSensitive: false, TrimWhitespace: true),
                null),
            QuestionType.ImagePoint => (
                new ImagePointInteractionDto("single-point"),
                new ImagePointAnswerKeyDto(
                [
                    new PolygonRegionDto("point", [new PointDto(0, 0)]),
                ]),
                null),
            QuestionType.LetterOrdering => (
                new LetterOrderingInteractionDto("reorder"),
                new LetterOrderingAnswerKeyDto(string.Empty, CaseSensitive: false),
                new LetterOrderingContentDto(string.Empty)),
            _ => (
                new SingleChoiceInteractionDto([new OptionDto("opt_a", "A", 1)]),
                new ChoiceAnswerKeyDto(["opt_a"]),
                null),
        };
    }
}
