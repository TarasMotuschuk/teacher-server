namespace Teacher.Common;

public sealed record TeacherUpdatePreparationProgress(
    TeacherUpdatePreparationStage Stage,
    string? Version,
    string Message,
    int? Percent,
    long? BytesTransferred,
    long? TotalBytes);
