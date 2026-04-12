namespace Teacher.Common;

public static class LocalDistributionOpenPaths
{
    /// <summary>
    /// Remote path to the uploaded file or folder root for shell open after bulk distribution.
    /// </summary>
    public static string GetRemotePathToOpenAfterDistribution(LocalDistributionPlan plan)
    {
        if (plan.Files.Count == 1)
        {
            var f = plan.Files[0];
            return RemoteWindowsPath.Combine(f.RemoteDirectory, Path.GetFileName(f.LocalPath));
        }

        return RemoteWindowsPath.CombineSegments(plan.DestinationRoot, plan.EntryName);
    }
}
