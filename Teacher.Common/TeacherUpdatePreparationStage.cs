namespace Teacher.Common;

public enum TeacherUpdatePreparationStage
{
    Idle = 0,
    Checking = 1,
    ReadyToDownload = 2,
    Downloading = 3,
    Prepared = 4,
    Failed = 5,
}
