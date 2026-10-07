using ClassCommander.Testing.Core.Packaging;

namespace ClassCommander.Testing.Core.Import;

public sealed class MyTestBatchConverter(IMyTestMtfConverter? mtfConverter = null)
{
    public async Task<MyTestConversionResult> ConvertAsync(
        string sourcePath, string outputDirectory, CancellationToken cancellationToken = default)
    {
        var workspace = Path.Combine(Path.GetTempPath(), "ClassCommander", "mytest-import", Guid.NewGuid().ToString("N"));
        string? stagedPackage = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var isMtf = Path.GetExtension(sourcePath).Equals(".mtf", StringComparison.OrdinalIgnoreCase);
            if (!isMtf && !Path.GetExtension(sourcePath).Equals(".xml", StringComparison.OrdinalIgnoreCase))
            {
                throw new MyTestImportException("unsupported-file");
            }

            Directory.CreateDirectory(workspace);
            var original = Path.Combine(workspace, isMtf ? "source.mtf" : "source.xml");
            File.Copy(sourcePath, original);
            var xmlPath = isMtf && mtfConverter is not null
                ? await (mtfConverter ?? throw new MyTestImportException("mtf-export-required")).ConvertAsync(original, workspace, cancellationToken)
                : original;
            await using var stream = File.OpenRead(xmlPath);
            var (definition, warnings) = isMtf && mtfConverter is null
                ? new MyTestMtfImporter().Import(stream, Path.GetFileName(sourcePath), workspace, cancellationToken)
                : new MyTestXmlImporter().Import(stream, Path.GetFileName(sourcePath), workspace);
            var questionCount = definition.Groups.Sum(g => g.Questions.Count);
            if (isMtf)
                definition = definition with { Source = definition.Source! with { Kind = "mytest-mtf" } };
            if (warnings.Count > 0)
            {
                return new(sourcePath, null, questionCount, warnings, "review-required", null);
            }

            Directory.CreateDirectory(outputDirectory);
            stagedPackage = Path.Combine(outputDirectory, $".{Guid.NewGuid():N}.tmp");
            await new CctestPackageService().SaveAsync(stagedPackage, definition,
                Path.Combine(workspace, "assets"), sourcePath, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var stem = Path.GetFileNameWithoutExtension(sourcePath);
            var destination = Path.Combine(outputDirectory, stem + ".cctest");

            // Move without replacement also protects packages created by another concurrent import.
            for (var suffix = 2; ; suffix++)
            {
                try
                {
                    File.Move(stagedPackage, destination, overwrite: false);
                    break;
                }
                catch (IOException) when (File.Exists(destination))
                {
                    destination = Path.Combine(outputDirectory, $"{stem} ({suffix}).cctest");
                }
            }

            return new(sourcePath, destination, questionCount, warnings, null, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (MyTestImportException ex)
        {
            return new(sourcePath, null, 0, [], ex.Code, ex.Detail);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException or InvalidOperationException or FormatException
            or ArgumentException or NotSupportedException or System.ComponentModel.Win32Exception
            or SixLabors.ImageSharp.UnknownImageFormatException or SixLabors.ImageSharp.InvalidImageContentException)
        {
            return new(sourcePath, null, 0, [], "conversion-failed", ex.GetType().Name);
        }
        finally
        {
            try
            {
                if (stagedPackage is not null && File.Exists(stagedPackage))
                {
                    File.Delete(stagedPackage);
                }

                if (Directory.Exists(workspace))
                {
                    Directory.Delete(workspace, recursive: true);
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
