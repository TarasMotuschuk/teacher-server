namespace Teacher.Common.Contracts;

public enum WindowsRestrictionKind
{
    TaskManager = 0,
    RunDialog = 1,
    ControlPanelAndSettings = 2,
    LockWorkstation = 3,
    ChangePassword = 4,
}

public sealed record WindowsRestrictionStateRequest(
    WindowsRestrictionKind Restriction,
    bool Enabled);
