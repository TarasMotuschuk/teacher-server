namespace ClassCommander.Testing.Core.Import;

public interface IMyTestMtfConverter
{
    Task<string> ConvertAsync(string sourcePath, string workingDirectory, CancellationToken cancellationToken);
}
