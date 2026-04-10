namespace Teacher.Common;

public enum TeacherClientUpdateStage
{
    Idle = 0,
    Checking = 1,
    Available = 2,
    UpToDate = 3,
    Downloading = 4,
    ReadyToInstall = 5,
    Failed = 6,
}
