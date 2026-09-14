using Teacher.Common.Contracts.Testing;

namespace ClassCommander.TestPlatform.Scoring;

internal static class AttemptScoringService
{
    public static ResultDto Score(TestDefinitionDto definition, string attemptPublicId, IReadOnlyList<AttemptAnswerDto> answers)
    {
        var questions = definition.Groups.SelectMany(group => group.Questions).ToList();
        var answerMap = answers
            .GroupBy(a => a.QuestionId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);

        var questionResults = new List<QuestionResultDto>();
        decimal earned = 0;
        decimal max = 0;

        foreach (var question in questions)
        {
            max += question.Score;
            answerMap.TryGetValue(question.Id, out var answer);
            var isCorrect = answer is not null && IsCorrect(question, answer.Value);
            var scoreEarned = isCorrect ? question.Score : 0m;
            earned += scoreEarned;
            questionResults.Add(new QuestionResultDto(question.Id, isCorrect, scoreEarned, question.Score));
        }

        var percent = max <= 0 ? 0m : Math.Round((earned / max) * 100m, 2);
        return new ResultDto(
            attemptPublicId,
            earned,
            max,
            percent,
            Grade: null,
            DateTime.UtcNow,
            questionResults);
    }

    private static bool IsCorrect(QuestionDto question, AttemptAnswerValueDto value)
    {
        return (question.AnswerKey, value) switch
        {
            (ChoiceAnswerKeyDto key, SingleChoiceAnswerValueDto answer) =>
                SameSet(key.CorrectOptionIds, answer.SelectedOptionIds),
            (ChoiceAnswerKeyDto key, MultipleChoiceAnswerValueDto answer) =>
                SameSet(key.CorrectOptionIds, answer.SelectedOptionIds),
            (OrderingAnswerKeyDto key, OrderingAnswerValueDto answer) =>
                key.CorrectOrder.SequenceEqual(answer.OrderedOptionIds, StringComparer.OrdinalIgnoreCase),
            (MatchingAnswerKeyDto key, MatchingAnswerValueDto answer) =>
                SameMatchingPairs(key.Pairs, answer.Pairs),
            (TrueFalseGroupAnswerKeyDto key, TrueFalseGroupAnswerValueDto answer) =>
                SameTruth(key.StatementTruth, answer.StatementTruth),
            (NumericInputGroupAnswerKeyDto key, NumericInputGroupAnswerValueDto answer) =>
                SameNumbers(key.Values, answer.Entries),
            (TextInputAnswerKeyDto key, TextInputAnswerValueDto answer) =>
                MatchesText(key, answer.Text),
            (ImagePointAnswerKeyDto key, ImagePointAnswerValueDto answer) =>
                IsPointInAnyRegion(key.Regions, answer.X, answer.Y),
            (LetterOrderingAnswerKeyDto key, LetterOrderingAnswerValueDto answer) =>
                string.Equals(
                    key.CaseSensitive ? key.TargetWord : key.TargetWord.ToLowerInvariant(),
                    key.CaseSensitive ? answer.Text : answer.Text.ToLowerInvariant(),
                    StringComparison.Ordinal),
            _ => false,
        };
    }

    private static bool SameSet(IReadOnlyList<string> left, IReadOnlyList<string> right) =>
        left.Count == right.Count
        && left.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .SequenceEqual(right.OrderBy(x => x, StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);

    private static bool SameMatchingPairs(IReadOnlyList<MatchingPairDto> expected, IReadOnlyList<MatchingAnswerValuePairDto> actual)
    {
        if (expected.Count != actual.Count)
        {
            return false;
        }

        var expectedSet = expected
            .Select(p => $"{p.LeftId}|{p.RightId}")
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
        var actualSet = actual
            .Select(p => $"{p.LeftId}|{p.RightId}")
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
        return expectedSet.SequenceEqual(actualSet, StringComparer.OrdinalIgnoreCase);
    }

    private static bool SameTruth(IReadOnlyList<StatementTruthDto> expected, IReadOnlyList<StatementTruthAnswerValueDto> actual)
    {
        if (expected.Count != actual.Count)
        {
            return false;
        }

        var actualMap = actual.ToDictionary(x => x.StatementId, x => x.Value, StringComparer.OrdinalIgnoreCase);
        return expected.All(item => actualMap.TryGetValue(item.StatementId, out var value) && value == item.Value);
    }

    private static bool SameNumbers(IReadOnlyList<AcceptedNumberDto> expected, IReadOnlyList<NumericEntryAnswerValueDto> actual)
    {
        var actualMap = actual.ToDictionary(x => x.EntryId, x => x.Value, StringComparer.OrdinalIgnoreCase);
        return expected.All(item =>
            actualMap.TryGetValue(item.EntryId, out var value)
            && item.AcceptedNumbers.Any(accepted => accepted == value));
    }

    private static bool MatchesText(TextInputAnswerKeyDto key, string text)
    {
        var normalized = key.TrimWhitespace ? text.Trim() : text;
        return key.AcceptedTexts.Any(accepted =>
        {
            var candidate = key.TrimWhitespace ? accepted.Trim() : accepted;
            return key.CaseSensitive
                ? string.Equals(candidate, normalized, StringComparison.Ordinal)
                : string.Equals(candidate, normalized, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static bool IsPointInAnyRegion(IReadOnlyList<PolygonRegionDto> regions, int x, int y) =>
        regions.Any(region => region.Points.Count >= 3 && IsPointInPolygon(region.Points, x, y));

    private static bool IsPointInPolygon(IReadOnlyList<PointDto> polygon, int x, int y)
    {
        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var pi = polygon[i];
            var pj = polygon[j];
            var intersect = ((pi.Y > y) != (pj.Y > y))
                && (x < ((pj.X - pi.X) * (y - pi.Y) / (double)(pj.Y - pi.Y + 0.0000001)) + pi.X);
            if (intersect)
            {
                inside = !inside;
            }
        }

        return inside;
    }
}
