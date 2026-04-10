namespace Teacher.Common;

public sealed record TeacherClientUpdateProgress(
    TeacherClientUpdateStage Stage,
    string Message,
    int? Percent,
    long? BytesTransferred,
    long? TotalBytes);
