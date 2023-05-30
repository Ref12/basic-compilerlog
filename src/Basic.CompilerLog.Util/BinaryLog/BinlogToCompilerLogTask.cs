using Basic.CompilerLog.Util;
using Microsoft.Build.Framework;
using Task = Microsoft.Build.Utilities.Task;

namespace Basic.CompilerLog.Tasks
{
    public class BinlogToCompilerLog : Task
    {
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        [Required]
        public ITaskItem BinlogPath { get; set; }

        [Required]
        public ITaskItem CompilerLogPath { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        public bool IncludeSatelliteAssemblies { get; set; }

        public string[] ProjectNames { get; set; } = Array.Empty<string>();

        public string[] TargetFrameworks { get; } = Array.Empty<string>();

        public override bool Execute()
        {
            var binlogPath = BinlogPath.GetMetadata("FullPath");
            var compilerLogPath = CompilerLogPath.GetMetadata("FullPath");

            var projectNameSet = new HashSet<string>(ProjectNames, StringComparer.OrdinalIgnoreCase);
            var targetFrameworkSet = new HashSet<string>(TargetFrameworks, StringComparer.OrdinalIgnoreCase);

            // Use the binary log reader for the current msbuild
            BinaryLogUtil.UseMSBuildDependentBinaryLogReader = true;

            var diagnostics = CompilerLogUtil.ConvertBinaryLog(
                binlogPath,
                compilerLogPath,
                call => FilterCompilerCalls(call, projectNameSet, targetFrameworkSet));

            foreach (var diagnostic in diagnostics)
            {
                Log.LogWarning(diagnostic);
            }

            return true;
        }
        internal bool FilterCompilerCalls(CompilerCall compilerCall, HashSet<string> projectNameSet, HashSet<string> targetFrameworkSet)
        {
            if (!IncludeSatelliteAssemblies && compilerCall.Kind == CompilerCallKind.Satellite)
            {
                return false;
            }

            if (targetFrameworkSet.Count > 0 && !targetFrameworkSet.Contains(compilerCall.TargetFramework, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            if (projectNameSet.Count > 0)
            {
                if (projectNameSet.Contains(Path.GetFileName(compilerCall.ProjectFilePath)) ||
                    projectNameSet.Contains(Path.GetFileNameWithoutExtension(compilerCall.ProjectFilePath)))
                {
                    return true;
                }

                return false;
            }

            return true;
        }
    }
}
