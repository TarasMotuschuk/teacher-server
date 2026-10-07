using System.Reflection;
using System.Text;
using ClassCommander.Testing.Core.Import;
using ClassCommander.Testing.Core.Packaging;
using Teacher.Common.Contracts.Testing;
using Teacher.Common.Localization;

namespace ClassCommander.TestRunner.SmokeTests;

internal static class MyTestImportChecks
{
    public static void Run()
    {
        var path = Path.Combine(Path.GetTempPath(), $"classcommander-invalid-{Guid.NewGuid():N}.xml");
        File.WriteAllText(path, "<broken>");
        var editor = new ClassCommander.TestEditor.MainWindow();
        try
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var field = editor.GetType().GetField("_definition", flags)!;
            var original = field.GetValue(editor);
            var import = (Task)editor.GetType().GetMethod("ImportMyTestAsync", flags)!.Invoke(editor, [path])!;
            try
            {
                import.GetAwaiter().GetResult();
                throw new InvalidOperationException("Broken XML must fail.");
            }
            catch (System.Xml.XmlException) { }

            Require(ReferenceEquals(original, field.GetValue(editor)), "A failed single import must preserve the open test.");
        }
        finally
        {
            editor.Close();
            File.Delete(path);
        }

        Task.Run(RunAsync).GetAwaiter().GetResult();
    }

    private static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "classcommander-mytest-checks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await MyTestNativeChecks.RunAsync(root);
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var xml = """
                <?xml version="1.0" encoding="windows-1251"?>
                <MyTest><TestOptions><Title>Український тест</Title></TestOptions><Groups><Group><Title>Тема</Title><Tasks>
                <Task Type="TYPE_TASK_CHOICE_SINGLE" Score="2"><QuestionText><PlainText>Столиця України?</PlainText></QuestionText><Variants>
                <VariantText CorrectAnswer="true"><PlainText>Київ</PlainText></VariantText>
                <VariantText CorrectAnswer="false"><PlainText>Львів</PlainText></VariantText>
                </Variants></Task></Tasks></Group></Groups></MyTest>
                """;
            var input = Path.Combine(root, "lesson.xml");
            var bytes = Encoding.GetEncoding(1251).GetBytes(xml);
            await File.WriteAllBytesAsync(input, bytes);
            var output = Path.Combine(root, "output");
            Directory.CreateDirectory(output);
            var existing = Path.Combine(output, "lesson.cctest");
            await File.WriteAllTextAsync(existing, "existing package");
            var converter = new MyTestBatchConverter();
            var success = await converter.ConvertAsync(input, output);
            Require(success.Succeeded && success.QuestionCount == 1, "Valid MyTest export must convert.");
            Require(success.PackagePath == Path.Combine(output, "lesson (2).cctest"), "Do not overwrite an existing package.");
            Require(await File.ReadAllTextAsync(existing) == "existing package", "Existing package content must survive.");
            Require((await File.ReadAllBytesAsync(input)).SequenceEqual(bytes), "Source must stay byte-for-byte unchanged.");
            var opened = await new CctestPackageService().OpenAsync(success.PackagePath!, Path.Combine(root, "opened"));
            Require(opened.Definition.Title == "Український тест", "Honor the declared Windows-1251 encoding.");
            var question = opened.Definition.Groups.Single().Questions.Single();
            Require(question.Prompt == "Столиця України?" && question.Score == 2
                && ((ChoiceAnswerKeyDto)question.AnswerKey).CorrectOptionIds.SequenceEqual(["opt_1"]), "Preserve prompt, score and correct answer.");
            Require(File.Exists(Path.Combine(opened.ExtractedDirectory, "imports", "lesson.xml")), "Keep original XML in the package.");

            var converterXml = """
                <MyTestX><TestOptions><Title>Converter fixture</Title></TestOptions><Groups><Group><Tasks>
                <Task Type="TYPE_TASK_CHOICE_SINGLE"><QuestionText><PlainText>????</PlainText><RTF>{\rtf1\ansi\ansicpg1251\uc1 \u1050?\u1080?\u1111?\u1074?}</RTF></QuestionText><Variants>
                <VariantText CorrectAnswer="True"><PlainText>????</PlainText><RTF>{\rtf1\ansi\ansicpg1251 \'ca\'e8\'bf\'e2}</RTF></VariantText>
                <VariantText CorrectAnswer="False"><PlainText>Other</PlainText></VariantText></Variants></Task>
                <Task Type="TYPE_TASK_ENTER_NUM"><QuestionText><PlainText>Number?</PlainText></QuestionText><InputNum><Value>10.8</Value></InputNum></Task>
                <Task Type="TYPE_TASK_ENTER_TEXT"><QuestionText><PlainText>Text?</PlainText></QuestionText><InputText IsCase="true"><Value>піксель</Value></InputText></Task>
                <Task Type="TYPE_TASK_CHOICE_COLLATION"><QuestionText><PlainText>Match</PlainText></QuestionText><Variants>
                <VariantText CorrectAnswer="2"><PlainText>Left A</PlainText></VariantText><VariantText CorrectAnswer="1"><PlainText>Left B</PlainText></VariantText></Variants><Variants2>
                <VariantText><PlainText>Right A</PlainText></VariantText><VariantText><PlainText>Right B</PlainText></VariantText></Variants2></Task>
                </Tasks></Group></Groups></MyTestX>
                """;
            var fakeMtf = Path.Combine(root, "source.mtf");
            await File.WriteAllTextAsync(fakeMtf, "original MTF bytes");
            var bridged = await new MyTestBatchConverter(new FixtureMtfConverter(converterXml)).ConvertAsync(fakeMtf, output);
            Require(bridged.Succeeded && bridged.QuestionCount == 4, "Bridge output must use the same validated XML importer.");
            var imported = await new CctestPackageService().OpenAsync(bridged.PackagePath!, Path.Combine(root, "bridged"));
            var importedQuestions = imported.Definition.Groups.Single().Questions;
            Require(importedQuestions[0].Prompt == "Київ"
                && ((SingleChoiceInteractionDto)importedQuestions[0].Interaction).Options[0].Text == "Київ", "Recover Unicode and code-page RTF instead of damaged plain text.");
            Require(((NumericInputGroupAnswerKeyDto)importedQuestions[1].AnswerKey).Values.Single().AcceptedNumbers.Single() == 10.8m, "Read InputNum/Value.");
            var textKey = (TextInputAnswerKeyDto)importedQuestions[2].AnswerKey;
            Require(textKey.CaseSensitive && textKey.AcceptedTexts.Single() == "піксель", "Read InputText/Value and case sensitivity.");
            Require(((MatchingInteractionDto)importedQuestions[3].Interaction).RightItems[0].Text == "Right A"
                && ((MatchingAnswerKeyDto)importedQuestions[3].AnswerKey).Pairs[0].RightId == "right_2", "Read the separate Variants2 column and its mapping.");
            Require(imported.Definition.Source?.Kind == "mytest-mtf"
                && await File.ReadAllTextAsync(Path.Combine(imported.ExtractedDirectory, "imports", "source.mtf")) == "original MTF bytes", "Retain MTF provenance and original bytes.");

            foreach (var (name, content, code) in new[]
            {
                ("unknown", xml.Replace("TYPE_TASK_CHOICE_SINGLE", "UNKNOWN_TYPE"), "unsupported-type"),
                ("key", xml.Replace("CorrectAnswer=\"true\"", "CorrectAnswer=\"false\""), "invalid-answer-key"),
                ("ordering", xml.Replace("TYPE_TASK_CHOICE_SINGLE", "TYPE_TASK_CHOICE_ORDER"), "invalid-answer-key"),
                ("empty", "<Other><Groups/></Other>", "no-questions"),
                ("dtd", "<!DOCTYPE MyTest [<!ENTITY external SYSTEM 'file:///etc/passwd'>]><MyTest>&external;</MyTest>", "conversion-failed"),
                ("broken", "<broken>", "conversion-failed"),
            })
            {
                var badInput = Path.Combine(root, name + ".xml");
                await File.WriteAllBytesAsync(badInput, Encoding.GetEncoding(1251).GetBytes(content));
                var failure = await converter.ConvertAsync(badInput, output);
                Require(!failure.Succeeded && failure.ErrorCode == code, $"Reject {name} with an actionable error.");
                Require(!File.Exists(Path.Combine(output, name + ".cctest")), "Failed conversion must not leave a package.");
            }
            var mtf = await converter.ConvertAsync(fakeMtf, output);
            Require(mtf.ErrorCode == "mtf-protected-or-invalid", "Reject invalid MTF input without launching external tools.");
            var warningInput = Path.Combine(root, "missing-image.xml");
            await File.WriteAllBytesAsync(warningInput, Encoding.GetEncoding(1251).GetBytes(
                xml.Replace("</QuestionText>", "</QuestionText><QuestionImage>invalid-base64</QuestionImage>")));
            var warning = await converter.ConvertAsync(warningInput, output);
            Require(!warning.Succeeded && warning.ErrorCode == "review-required" && warning.Warnings.Count == 1,
                "A lost question image must prevent automatic packaging.");
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            try
            {
                await converter.ConvertAsync(input, output, cancellation.Token);
                throw new InvalidOperationException("Cancellation must propagate.");
            }
            catch (OperationCanceledException) { }

            Require(Directory.GetFiles(output, "*.tmp").Length == 0, "Cancellation must clean staged packages.");
            Require((await converter.ConvertAsync(input, output)).Succeeded, "One failed file must not prevent subsequent conversions.");
            foreach (var language in new[] { UiLanguage.English, UiLanguage.Ukrainian })
            {
                var text = typeof(ClassCommander.TestEditor.MainWindow).Assembly.GetType("ClassCommander.TestEditor.Localization.TestEditorText")!;
                text.GetProperty("Language")!.SetValue(null, language);
                var guidance = (string)text.GetMethod("ImportError")!.Invoke(null, [mtf.ErrorCode])!;
                Require(guidance.Contains("XML", StringComparison.Ordinal), "Both languages must explain the MTF migration route.");
            }

            Console.WriteLine("PASS: MyTest batch conversion, encoding, answer keys, source preservation, collisions, cancellation and invalid XML.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class FixtureMtfConverter(string xml) : IMyTestMtfConverter
    {
        public async Task<string> ConvertAsync(string sourcePath, string workingDirectory, CancellationToken cancellationToken)
        {
            Require(await File.ReadAllTextAsync(sourcePath, cancellationToken) == "original MTF bytes", "Convert a source copy.");
            var path = Path.Combine(workingDirectory, "converted.xml");
            await File.WriteAllTextAsync(path, xml, cancellationToken);
            return path;
        }
    }
}
