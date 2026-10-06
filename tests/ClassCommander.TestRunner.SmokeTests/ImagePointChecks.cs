using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ClassCommander.TestEditor.Services;
using ClassCommander.Testing.Core.Packaging;
using ClassCommander.TestPlatform.Scoring;
using Teacher.Common.Contracts.Testing;

namespace ClassCommander.TestRunner.SmokeTests;

internal static class ImagePointChecks
{
    public static void Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "classcommander-image-point-smoke");
        Directory.CreateDirectory(directory);
        var imagePath = Path.Combine(directory, "source.png");
        using (var bitmap = new WriteableBitmap(new PixelSize(200, 100), new Vector(96, 96)))
        {
            bitmap.Save(imagePath);
        }

        var asset = new QuestionAssetDto("picture", AssetKind.Image, "image/png", imagePath, 200, 100, null);
        using var editor = new ClassCommander.TestEditor.Services.ImagePointEditor(new ImagePointAnswerKeyDto([]), asset, imagePath, null);
        var window = new Window { Content = editor, Width = 500, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var picker = editor.GetVisualDescendants().OfType<Control>().First(control => control.GetType().Name == "ImagePointPicker");
        var center = new Point(picker.Bounds.Width / 2, picker.Bounds.Height / 2);
        var selected = (PointDto)picker.GetType().GetMethod("PointAt")!.Invoke(picker, [center])!;
        Require(selected == new PointDto(100, 50), "Scaled clicks must map to original image pixels.");
        Require(picker.GetType().GetMethod("PointAt")!.Invoke(picker, [new Point(-1, -1)]) is null, "Clicks outside the image must not select an answer.");
        var mode = editor.GetVisualDescendants().OfType<ComboBox>().Single();
        mode.SelectedIndex = 1;
        picker.GetType().GetMethod("SelectPoint")!.Invoke(picker, [selected]);
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()!.Save(Path.Combine(directory, "editor.png"));
        var key = editor.CollectAnswerKey();
        var question = QuestionFactory.Create(QuestionType.ImagePoint) with { Assets = [new QuestionAssetRefDto(asset.Id, "prompt")], AnswerKey = key };
        var definition = new TestDefinitionDto(1, "test-definition", "image_test", 1, "Image test", null, "en", null, [], [], null, null,
            new TestSettingsDto(false, false, false, null, 1, "score-only"), [asset], [new TestGroupDto("group", "Group", null, 0, [question])]);
        decimal Score(int x, int y) => AttemptScoringService.Score(definition, "attempt", [new AttemptAnswerDto(question.Id, QuestionType.ImagePoint, new ImagePointAnswerValueDto(x, y), true, DateTime.UtcNow)]).ScoreEarned;
        Require(Score(100, 50) == 1 && Score(110, 60) == 1 && Score(111, 60) == 0, "The selected point and tolerance boundary must score correctly.");
        var legacy = definition with { Groups = [definition.Groups[0] with { Questions = [question with { AnswerKey = new ImagePointAnswerKeyDto([new PolygonRegionDto("point", [selected])]) }] }] };
        Require(AttemptScoringService.Score(legacy, "attempt", [new AttemptAnswerDto(question.Id, QuestionType.ImagePoint, new ImagePointAnswerValueDto(100, 50), true, DateTime.UtcNow)]).ScoreEarned == 1, "Existing single-point keys must score correctly.");
        mode.SelectedIndex = 0;
        Dispatcher.UIThread.RunJobs();
        Point Position(int x, int y)
        {
            var scale = Math.Min(picker.Bounds.Width / 200, picker.Bounds.Height / 100);
            return picker.TranslatePoint(new Point(((picker.Bounds.Width - (200 * scale)) / 2) + (x * scale), ((picker.Bounds.Height - (100 * scale)) / 2) + (y * scale)), window)!.Value;
        }

        var start = Position(150, 70);
        var end = Position(50, 30);
        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(end, Avalonia.Input.RawInputModifiers.LeftMouseButton);
        Require(editor.CollectAnswerKey().Regions[0].Points.SequenceEqual(key.Regions[0].Points), "Dragging a preview must not save before release.");
        window.MouseUp(end, MouseButton.Left);
        var rectangle = editor.CollectAnswerKey();
        Require(rectangle.Regions[0].Points.SequenceEqual(new PointDto[] { new(50, 30), new(150, 30), new(150, 70), new(50, 70) }), "Reverse dragging must create a normalized original-pixel rectangle.");
        question = question with { AnswerKey = rectangle };
        definition = definition with { Groups = [definition.Groups[0] with { Questions = [question] }] };
        Require(Score(50, 30) == 1 && Score(150, 70) == 1 && Score(70, 60) == 1 && Score(151, 70) == 0, "Clicks anywhere within the rectangle, including edges, must score correctly.");
        window.MouseDown(Position(90, 40), MouseButton.Left);
        window.MouseUp(Position(90, 40), MouseButton.Left);
        Require(editor.CollectAnswerKey().Regions[0].Points.SequenceEqual(rectangle.Regions[0].Points), "A click without a rectangle must preserve the previous answer area.");
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()!.Save(Path.Combine(directory, "editor-area.png"));
        var package = new CctestPackageService();
        var packagePath = Path.Combine(directory, "image.cctest");
        Task.Run(() => package.SaveAsync(packagePath, definition, directory)).GetAwaiter().GetResult();
        var opened = Task.Run(() => package.OpenAsync(packagePath)).GetAwaiter().GetResult();
        Require(opened.Definition.Assets[0].Width == 200 && opened.Definition.Assets[0].Height == 100, "Packaging must preserve coordinate dimensions.");
        using (var reopened = new ClassCommander.TestEditor.Services.ImagePointEditor(
            (ImagePointAnswerKeyDto)opened.Definition.Groups[0].Questions[0].AnswerKey,
            opened.Definition.Assets[0], Path.Combine(opened.ExtractedDirectory, opened.Definition.Assets[0].Path), null))
        {
            Require(reopened.CollectAnswerKey().Regions[0].Points.SequenceEqual(rectangle.Regions[0].Points), "Reopening a package must preserve the selected rectangle.");
        }

        var packagedImage = File.ReadAllBytes(Path.Combine(opened.ExtractedDirectory, opened.Definition.Assets[0].Path));
        using var runner = new ClassCommander.TestRunner.ImagePointEditor(null, packagedImage);
        window.Content = runner.Control;
        window.Width = 350;
        Dispatcher.UIThread.RunJobs();
        var runnerPicker = runner.Control.GetVisualDescendants().OfType<Control>().First(control => control.GetType().Name == "ImagePointPicker");
        var studentScale = Math.Min(runnerPicker.Bounds.Width / 200, runnerPicker.Bounds.Height / 100);
        var studentPosition = runnerPicker.TranslatePoint(
            new Point(
            ((runnerPicker.Bounds.Width - (200 * studentScale)) / 2) + (80 * studentScale),
            ((runnerPicker.Bounds.Height - (100 * studentScale)) / 2) + (60 * studentScale)), window)!.Value;
        window.MouseDown(studentPosition, MouseButton.Left);
        window.MouseUp(studentPosition, MouseButton.Left);
        Require(
            runner.Collect() is ImagePointAnswerValueDto { X: 80, Y: 60 } && Score(80, 60) == 1,
            "A student click away from the center must score correctly after image resizing.");
        Require(!runner.Control.GetVisualDescendants().OfType<TextBox>().Any(), "The student must not enter coordinates manually.");
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()!.Save(Path.Combine(directory, "student.png"));
        window.Close();
        Console.WriteLine("PASS: image area drag/preview, scaling, boundaries, package round trip, student selection, point tolerance and legacy scoring.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
