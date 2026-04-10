namespace Teacher.Common.Contracts;

public enum AgentUpdateStateKind
{
    Idle = 0,
    Checking = 1,
    UpToDate = 2,
    Available = 3,
    Downloading = 4,
    Installing = 5,
    Succeeded = 6,
    Failed = 7,
    RolledBack = 8,
}
