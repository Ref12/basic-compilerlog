namespace Basic.CompilerLog.Util;

public static class BinaryLogUtil
{
    /// <summary>
    /// This global setting indicates that StructuredLogger.dll should be used to read binary logs.
    /// Default is false.
    /// </summary>
    /// <remarks>
    /// This is normally the safest option, but in contexts where MSBuild version of log may be newer
    /// and the Microsoft.Build.dll binary can be obtained using the MSBuild dependent version may make
    /// more sense (i.e. running inside any MSBuild task).
    /// </remarks>
    public static bool UseMSBuildDependentBinaryLogReader { get; set; } = false;

    public static List<CompilerCall> ReadAllCompilerCalls(
        Stream stream,
        List<string> diagnosticList,
        Func<CompilerCall, bool>? predicate = null)
    {
        predicate ??= static _ => true;
        BinaryLogCompilerCallCollectorBase collector = UseMSBuildDependentBinaryLogReader
            ? new MSBuildBinaryLogCompilerCallCollector(diagnosticList, predicate)
            : new StructuredLoggerCompilerCallCollector(diagnosticList, predicate);

        return collector.Collect(stream);
    }

    /// <summary>
    /// The argument list is going to include either `dotnet exec csc.dll` or `csc.exe`. Need 
    /// to skip past that to get to the real command line.
    /// </summary>
    internal static IEnumerable<string> SkipCompilerExecutable(IEnumerable<string> args, string exeName, string dllName)
    {
        using var e = args.GetEnumerator();

        // The path to the executable is not escaped like the other command line arguments. Need
        // to skip until we see an exec or a path with the exe as the file name.
        var found = false;
        while (e.MoveNext())
        {
            if (PathUtil.Comparer.Equals(e.Current, "exec"))
            {
                if (e.MoveNext() && PathUtil.Comparer.Equals(Path.GetFileName(e.Current), dllName))
                {
                    found = true;
                }
                break;
            }
            else if (e.Current.EndsWith(exeName, PathUtil.Comparison))
            {
                found = true;
                break;
            }
        }

        if (!found)
        {
            yield break;
        }

        while (e.MoveNext())
        {
            yield return e.Current;
        }
    }
}
