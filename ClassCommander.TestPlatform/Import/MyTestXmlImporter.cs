using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ClassCommander.TestPlatform.Storage;
using Teacher.Common.Contracts.Testing;

namespace ClassCommander.TestPlatform.Import;

internal sealed class MyTestXmlImporter
{
    static MyTestXmlImporter()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private readonly TestPlatformPaths _paths;

    public MyTestXmlImporter(TestPlatformPaths paths)
    {
        _paths = paths;
    }

    public (TestDefinitionDto Definition, IReadOnlyList<string> Warnings) Import(Stream xmlStream, string? originalFileName)
    {
        var warnings = new List<string>();
        XDocument document;
        using (var reader = new StreamReader(xmlStream, detectEncodingFromByteOrderMarks: true, leaveOpen: true))
        {
            document = XDocument.Load(reader);
        }

        var root = document.Root ?? throw new InvalidOperationException("MyTest XML root element is missing.");
        var testOptions = root.Element("TestOptions");
        var title = TextValue(testOptions, "Title") ?? Path.GetFileNameWithoutExtension(originalFileName) ?? "Imported test";
        var publicId = TextValue(testOptions, "TestUID");
        if (string.IsNullOrWhiteSpace(publicId))
        {
            publicId = $"test_{Slugify(title)}";
        }

        var assetsDirectory = _paths.GetAssetsDirectory(publicId);
        Directory.CreateDirectory(assetsDirectory);
        Directory.CreateDirectory(_paths.GetImportsDirectory(publicId));

        var assets = new List<QuestionAssetDto>();
        var groups = new List<TestGroupDto>();
        var groupIndex = 0;
        foreach (var groupElement in root.Elements("Groups").Elements("Group"))
        {
            groupIndex++;
            var questions = new List<QuestionDto>();
            var taskIndex = 0;
            foreach (var task in groupElement.Elements("Tasks").Elements("Task"))
            {
                taskIndex++;
                questions.Add(ParseTask(task, groupIndex, taskIndex, publicId, assetsDirectory, assets, warnings));
            }

            groups.Add(new TestGroupDto(
                $"group_{groupIndex}",
                TextValue(groupElement, "Title") ?? $"Group {groupIndex}",
                PrimaryText(groupElement.Element("Description")),
                groupIndex,
                questions));
        }

        var definition = new TestDefinitionDto(
            SchemaVersion: 1,
            Type: "test-definition",
            PublicId: publicId,
            Version: 1,
            Title: title,
            Description: PrimaryText(testOptions?.Element("Description")),
            Language: "uk",
            Grade: null,
            Subjects: [],
            Tags: [],
            Author: string.IsNullOrWhiteSpace(TextValue(testOptions, "Author"))
                ? null
                : new AuthorDto(TextValue(testOptions, "Author")!, TextValue(testOptions, "AuthorEmail")),
            Source: new TestSourceDto(
                "mytest-xml",
                originalFileName,
                DateTime.UtcNow,
                new Dictionary<string, string>
                {
                    ["myTestVersion"] = TextValue(root, "Version") ?? string.Empty,
                    ["saveDate"] = TextValue(root, "SaveDate") ?? string.Empty,
                }),
            Settings: new TestSettingsDto(
                ShuffleQuestions: BoolValue(TextValue(testOptions, "IsOrderTaskRandom")) ?? false,
                ShuffleOptions: BoolValue(TextValue(testOptions, "IsOrderVariantRandom")) ?? false,
                ShowCorrectAnswersAfterFinish: false,
                TimeLimitSeconds: null,
                DefaultAttemptLimit: 1,
                ResultVisibility: "score-only"),
            Assets: assets,
            Groups: groups);

        return (definition, warnings);
    }

    private static QuestionDto ParseTask(
        XElement task,
        int groupIndex,
        int taskIndex,
        string testPublicId,
        string assetsDirectory,
        List<QuestionAssetDto> assets,
        List<string> warnings)
    {
        var questionId = $"q_{groupIndex}_{taskIndex}";
        var myTestType = task.Attribute("Type")?.Value ?? string.Empty;
        var score = decimal.TryParse(task.Attribute("Score")?.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedScore)
            ? parsedScore
            : 1m;
        var prompt = PrimaryText(task.Element("QuestionText")) ?? string.Empty;
        var variants = task.Elements("Variants").Elements("VariantText").ToList();
        var assetRefs = new List<QuestionAssetRefDto>();

        var imageElement = task.Element("QuestionImage");
        if (imageElement is not null && !string.IsNullOrWhiteSpace(imageElement.Value))
        {
            try
            {
                var asset = SaveImageAsset(imageElement, testPublicId, questionId, assetsDirectory);
                assets.Add(asset);
                assetRefs.Add(new QuestionAssetRefDto(asset.Id, "prompt"));
            }
            catch (Exception ex)
            {
                warnings.Add($"{questionId}: failed to import question image ({ex.Message}).");
            }
        }

        if (!TryMapType(myTestType, out var questionType))
        {
            warnings.Add($"{questionId}: unsupported MyTest type '{myTestType}', imported as single-choice.");
            questionType = QuestionType.SingleChoice;
        }

        var (content, interaction, answerKey, typeWarnings) = BuildQuestionParts(questionType, variants, task, prompt);
        warnings.AddRange(typeWarnings.Select(w => $"{questionId}: {w}"));

        return new QuestionDto(
            questionId,
            questionType,
            prompt,
            Description: null,
            score,
            Required: true,
            assetRefs,
            content,
            interaction,
            answerKey,
            new QuestionSourceDto(myTestType, typeWarnings));
    }

    private static (QuestionContentDto? Content, QuestionInteractionDto Interaction, QuestionAnswerKeyDto AnswerKey, List<string> Warnings)
        BuildQuestionParts(QuestionType type, List<XElement> variants, XElement task, string prompt)
    {
        var warnings = new List<string>();
        switch (type)
        {
            case QuestionType.SingleChoice:
            case QuestionType.MultipleChoice:
                {
                    var options = new List<OptionDto>();
                    var correct = new List<string>();
                    for (var i = 0; i < variants.Count; i++)
                    {
                        var optionId = $"opt_{i + 1}";
                        options.Add(new OptionDto(optionId, PrimaryText(variants[i]) ?? string.Empty, i + 1));
                        if (BoolValue(variants[i].Attribute("CorrectAnswer")?.Value) == true)
                        {
                            correct.Add(optionId);
                        }
                    }

                    QuestionInteractionDto interaction = type == QuestionType.SingleChoice
                        ? new SingleChoiceInteractionDto(options)
                        : new MultipleChoiceInteractionDto(options);
                    return (null, interaction, new ChoiceAnswerKeyDto(correct), warnings);
                }

            case QuestionType.Ordering:
                {
                    var options = new List<OptionDto>();
                    var orderPairs = new List<(string Id, int Order)>();
                    for (var i = 0; i < variants.Count; i++)
                    {
                        var optionId = $"opt_{i + 1}";
                        options.Add(new OptionDto(optionId, PrimaryText(variants[i]) ?? string.Empty, i + 1));
                        var correctRaw = variants[i].Attribute("CorrectAnswer")?.Value;
                        if (int.TryParse(correctRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var order))
                        {
                            orderPairs.Add((optionId, order));
                        }
                    }

                    var correctOrder = orderPairs.OrderBy(x => x.Order).Select(x => x.Id).ToList();
                    if (correctOrder.Count == 0)
                    {
                        warnings.Add("ordering answer key was empty; using source variant order.");
                        correctOrder = options.Select(o => o.Id).ToList();
                    }

                    return (null, new OrderingInteractionDto(options), new OrderingAnswerKeyDto(correctOrder), warnings);
                }

            case QuestionType.Matching:
                {
                    var leftItems = new List<MatchingItemDto>();
                    var rightItems = new List<MatchingItemDto>();
                    var pairs = new List<MatchingPairDto>();
                    for (var i = 0; i < variants.Count; i++)
                    {
                        var leftId = $"left_{i + 1}";
                        var rightId = $"right_{i + 1}";
                        var leftText = TextValue(variants[i], "PlainText") ?? PrimaryText(variants[i]) ?? string.Empty;
                        var rightText = TextValue(variants[i], "PlainText2") ?? leftText;
                        leftItems.Add(new MatchingItemDto(leftId, leftText, i + 1));
                        rightItems.Add(new MatchingItemDto(rightId, rightText, i + 1));

                        var correctRaw = variants[i].Attribute("CorrectAnswer")?.Value;
                        if (int.TryParse(correctRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rightIndex)
                            && rightIndex >= 1
                            && rightIndex <= variants.Count)
                        {
                            pairs.Add(new MatchingPairDto(leftId, $"right_{rightIndex}"));
                        }
                        else if (BoolValue(correctRaw) == true)
                        {
                            pairs.Add(new MatchingPairDto(leftId, rightId));
                        }
                    }

                    if (pairs.Count == 0)
                    {
                        warnings.Add("matching pairs were empty; pairing each left item to the same-index right item.");
                        pairs = leftItems.Select((left, index) => new MatchingPairDto(left.Id, rightItems[index].Id)).ToList();
                    }

                    return (null, new MatchingInteractionDto(leftItems, rightItems), new MatchingAnswerKeyDto(pairs), warnings);
                }

            case QuestionType.TrueFalseGroup:
                {
                    var statements = new List<StatementDto>();
                    var truth = new List<StatementTruthDto>();
                    for (var i = 0; i < variants.Count; i++)
                    {
                        var statementId = $"s_{i + 1}";
                        statements.Add(new StatementDto(statementId, PrimaryText(variants[i]) ?? string.Empty, i + 1));
                        truth.Add(new StatementTruthDto(statementId, BoolValue(variants[i].Attribute("CorrectAnswer")?.Value) ?? false));
                    }

                    return (null, new TrueFalseGroupInteractionDto(statements), new TrueFalseGroupAnswerKeyDto(truth), warnings);
                }

            case QuestionType.NumericInputGroup:
                {
                    var entries = new List<NumericEntryDto>();
                    var values = new List<AcceptedNumberDto>();
                    for (var i = 0; i < variants.Count; i++)
                    {
                        var entryId = $"n_{i + 1}";
                        entries.Add(new NumericEntryDto(entryId, PrimaryText(variants[i]) ?? string.Empty, i + 1));
                        var accepted = new List<decimal>();
                        var correctRaw = variants[i].Attribute("CorrectAnswer")?.Value;
                        if (decimal.TryParse(correctRaw, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                        {
                            accepted.Add(number);
                        }
                        else if (decimal.TryParse(PrimaryText(variants[i]), NumberStyles.Number, CultureInfo.InvariantCulture, out var fromText))
                        {
                            accepted.Add(fromText);
                        }

                        values.Add(new AcceptedNumberDto(entryId, accepted));
                    }

                    if (entries.Count == 0)
                    {
                        entries.Add(new NumericEntryDto("n_1", prompt, 1));
                        values.Add(new AcceptedNumberDto("n_1", []));
                        warnings.Add("numeric question had no variants; created a single empty entry.");
                    }

                    return (null, new NumericInputGroupInteractionDto(entries), new NumericInputGroupAnswerKeyDto(values), warnings);
                }

            case QuestionType.TextInput:
                {
                    var accepted = variants
                        .Select(v => v.Attribute("CorrectAnswer")?.Value)
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Select(v => v!.Trim())
                        .Distinct(StringComparer.Ordinal)
                        .ToList();
                    if (accepted.Count == 0)
                    {
                        accepted = variants
                            .Select(PrimaryText)
                            .Where(v => !string.IsNullOrWhiteSpace(v))
                            .Select(v => v!.Trim())
                            .Distinct(StringComparer.Ordinal)
                            .ToList();
                    }

                    return (
                        null,
                        new TextInputInteractionDto(Placeholder: null, MaxLength: null),
                        new TextInputAnswerKeyDto(accepted, CaseSensitive: false, TrimWhitespace: true),
                        warnings);
                }

            case QuestionType.ImagePoint:
                {
                    var regions = new List<PolygonRegionDto>();
                    foreach (var region in task.Elements("Regions").Elements("Region"))
                    {
                        var points = ParseRegionPoints(region.Value);
                        if (points.Count > 0)
                        {
                            regions.Add(new PolygonRegionDto("polygon", points));
                        }
                    }

                    if (regions.Count == 0)
                    {
                        warnings.Add("image-point question has no parseable regions.");
                    }

                    return (null, new ImagePointInteractionDto("single-point"), new ImagePointAnswerKeyDto(regions), warnings);
                }

            case QuestionType.LetterOrdering:
                {
                    var targetWord = variants
                        .Select(v => v.Attribute("CorrectAnswer")?.Value)
                        .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))
                        ?? TextValue(task.Element("QuestionText"), "PlainText2")
                        ?? string.Concat(variants.Select(PrimaryText).Where(v => !string.IsNullOrWhiteSpace(v)));
                    if (string.IsNullOrWhiteSpace(targetWord))
                    {
                        warnings.Add("letter-ordering target word was empty.");
                        targetWord = string.Empty;
                    }

                    return (
                        new LetterOrderingContentDto(targetWord),
                        new LetterOrderingInteractionDto("reorder-letters"),
                        new LetterOrderingAnswerKeyDto(targetWord, CaseSensitive: false),
                        warnings);
                }

            default:
                return (
                    null,
                    new SingleChoiceInteractionDto([]),
                    new ChoiceAnswerKeyDto([]),
                    warnings);
        }
    }

    private static QuestionAssetDto SaveImageAsset(
        XElement imageElement,
        string testPublicId,
        string questionId,
        string assetsDirectory)
    {
        var originalFileName = imageElement.Attribute("FileName")?.Value;
        var raw = imageElement.Value.Trim();
        var bytes = Convert.FromBase64String(NormalizeQuestionImage(raw));
        var assetId = $"asset_{questionId}";
        var extension = DetectImageExtension(bytes, originalFileName);
        var fileName = $"{assetId}{extension}";
        var absolutePath = Path.Combine(assetsDirectory, fileName);
        File.WriteAllBytes(absolutePath, bytes);

        return new QuestionAssetDto(
            assetId,
            AssetKind.Image,
            MimeFromExtension(extension),
            Path.Combine("tests", testPublicId, "assets", fileName).Replace('\\', '/'),
            Width: null,
            Height: null,
            new AssetSourceDto(originalFileName));
    }

    private static string NormalizeQuestionImage(string rawBase64)
    {
        try
        {
            var decoded = Convert.FromBase64String(rawBase64);
            var text = System.Text.Encoding.UTF8.GetString(decoded);
            var repaired = new List<byte>(text.Length);
            foreach (var ch in text)
            {
                var codepoint = (int)ch;
                if (codepoint <= 0xFF)
                {
                    repaired.Add((byte)codepoint);
                }
                else
                {
                    repaired.AddRange(System.Text.Encoding.GetEncoding(1251).GetBytes(new[] { ch }));
                }
            }

            var repairedBytes = repaired.ToArray();
            if (repairedBytes.Length >= 2 && repairedBytes[0] == (byte)'B' && repairedBytes[1] == (byte)'M')
            {
                return Convert.ToBase64String(repairedBytes);
            }
        }
        catch
        {
            // Fall back to the original payload.
        }

        return rawBase64;
    }

    private static string DetectImageExtension(byte[] bytes, string? originalFileName)
    {
        if (bytes.Length >= 2 && bytes[0] == (byte)'B' && bytes[1] == (byte)'M')
        {
            return ".bmp";
        }

        if (bytes.Length >= 8
            && bytes[0] == 0x89
            && bytes[1] == 0x50
            && bytes[2] == 0x4E
            && bytes[3] == 0x47)
        {
            return ".png";
        }

        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return ".jpg";
        }

        var ext = Path.GetExtension(originalFileName ?? string.Empty);
        return string.IsNullOrWhiteSpace(ext) ? ".bin" : ext.ToLowerInvariant();
    }

    private static string MimeFromExtension(string extension) => extension.ToLowerInvariant() switch
    {
        ".bmp" => "image/bmp",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        _ => "application/octet-stream",
    };

    private static IReadOnlyList<PointDto> ParseRegionPoints(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        var points = new List<PointDto>();
        foreach (Match match in Regex.Matches(raw, @"(-?\d+)\s*[,;]\s*(-?\d+)"))
        {
            if (int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)
                && int.TryParse(match.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
            {
                points.Add(new PointDto(x, y));
            }
        }

        return points;
    }

    private static bool TryMapType(string myTestType, out QuestionType questionType)
    {
        switch (myTestType.Trim().ToUpperInvariant())
        {
            case "TYPE_TASK_CHOICE_SINGLE":
                questionType = QuestionType.SingleChoice;
                return true;
            case "TYPE_TASK_CHOICE_MULTIPLE":
                questionType = QuestionType.MultipleChoice;
                return true;
            case "TYPE_TASK_CHOICE_ORDER":
                questionType = QuestionType.Ordering;
                return true;
            case "TYPE_TASK_CHOICE_COLLATION":
                questionType = QuestionType.Matching;
                return true;
            case "TYPE_TASK_CHOICE_TRUE_FALSE":
                questionType = QuestionType.TrueFalseGroup;
                return true;
            case "TYPE_TASK_ENTER_NUM":
                questionType = QuestionType.NumericInputGroup;
                return true;
            case "TYPE_TASK_ENTER_TEXT":
                questionType = QuestionType.TextInput;
                return true;
            case "TYPE_TASK_IMAGE_POINT":
                questionType = QuestionType.ImagePoint;
                return true;
            case "TYPE_TASK_WORD":
                questionType = QuestionType.LetterOrdering;
                return true;
            default:
                questionType = QuestionType.SingleChoice;
                return false;
        }
    }

    private static string? TextValue(XElement? parent, string tag) =>
        parent?.Element(tag)?.Value?.Trim();

    private static string? PrimaryText(XElement? parent)
    {
        if (parent is null)
        {
            return null;
        }

        return TextValue(parent, "PlainText")
            ?? TextValue(parent, "PlainText2")
            ?? parent.Value?.Trim();
    }

    private static bool? BoolValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "true" or "1" or "yes" => true,
            "false" or "0" or "no" => false,
            _ => null,
        };
    }

    private static string Slugify(string value)
    {
        var slug = Regex.Replace(value.Trim(), @"[^\w\-]+", "-", RegexOptions.CultureInvariant);
        slug = Regex.Replace(slug, "-{2,}", "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "test" : slug.ToLowerInvariant();
    }
}
