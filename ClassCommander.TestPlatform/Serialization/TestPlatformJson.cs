using System.Text.Json;
using System.Text.Json.Serialization;
using Teacher.Common.Contracts.Testing;

namespace ClassCommander.TestPlatform.Serialization;

internal static class TestPlatformJson
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        };

        return options;
    }

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options)
        ?? throw new InvalidOperationException($"Failed to deserialize {typeof(T).Name}.");

    public static TestDefinitionDto StripAnswerKeys(TestDefinitionDto definition)
    {
        var groups = definition.Groups
            .Select(group => group with
            {
                Questions = group.Questions
                    .Select(question => question with
                    {
                        AnswerKey = CreateEmptyAnswerKey(question.Type),
                    })
                    .ToList(),
            })
            .ToList();

        return definition with { Groups = groups };
    }

    private static QuestionAnswerKeyDto CreateEmptyAnswerKey(QuestionType type) => type switch
    {
        QuestionType.SingleChoice or QuestionType.MultipleChoice => new ChoiceAnswerKeyDto([]),
        QuestionType.Ordering => new OrderingAnswerKeyDto([]),
        QuestionType.Matching => new MatchingAnswerKeyDto([]),
        QuestionType.TrueFalseGroup => new TrueFalseGroupAnswerKeyDto([]),
        QuestionType.NumericInputGroup => new NumericInputGroupAnswerKeyDto([]),
        QuestionType.TextInput => new TextInputAnswerKeyDto([], CaseSensitive: false, TrimWhitespace: true),
        QuestionType.ImagePoint => new ImagePointAnswerKeyDto([]),
        QuestionType.LetterOrdering => new LetterOrderingAnswerKeyDto(string.Empty, CaseSensitive: false),
        _ => new ChoiceAnswerKeyDto([]),
    };
}
