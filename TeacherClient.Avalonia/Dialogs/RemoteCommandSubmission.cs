using Teacher.Common.Contracts;

namespace TeacherClient.CrossPlatform.Dialogs;

public sealed record RemoteCommandSubmission(string Script, RemoteCommandRunAs RunAs);
