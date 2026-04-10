namespace TeacherClient.CrossPlatform.Services;

public sealed record TransferProgress(long BytesTransferred, long? TotalBytes)
{
    public bool HasTotal => TotalBytes.HasValue && TotalBytes.Value > 0;

    public double ProgressRatio => HasTotal ? (double)BytesTransferred / TotalBytes!.Value : 0d;

    public int Percent => HasTotal ? (int)Math.Clamp(Math.Round(ProgressRatio * 100d), 0, 100) : 0;
}
