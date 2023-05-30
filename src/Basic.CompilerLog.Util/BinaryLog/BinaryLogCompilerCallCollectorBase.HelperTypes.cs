using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

namespace Basic.CompilerLog.Util;

using static BinaryLogUtil;

partial class BinaryLogCompilerCallCollectorBase
{
    private sealed class MSBuildProjectData
    {
        private readonly Dictionary<int, CompilationTaskData> _targetMap = new();
        public readonly string ProjectFile;
        public string? TargetFramework;
        public int? EvaluationId;

        public MSBuildProjectData(string projectFile)
        {
            ProjectFile = projectFile;
        }

        public bool TryGetTaskData(int targetId, [NotNullWhen(true)] out CompilationTaskData? data) =>
            _targetMap.TryGetValue(targetId, out data);

        public CompilationTaskData GetOrCreateTaskData(int targetId)
        {
            if (!_targetMap.TryGetValue(targetId, out var data))
            {
                data = new CompilationTaskData(this, targetId);
                _targetMap[targetId] = data;
            }

            return data;
        }

        public List<CompilerCall> GetAllCompilerCalls(List<string> diagnostics)
        {
            var list = new List<CompilerCall>();

            foreach (var data in _targetMap.Values)
            {
                if (data.TryCreateCompilerCall(diagnostics) is { } compilerCall)
                {
                    if (compilerCall.Kind == CompilerCallKind.Regular)
                    {
                        list.Insert(0, compilerCall);
                    }
                    else
                    {
                        list.Add(compilerCall);
                    }
                }
            }

            return list;
        }

        public override string ToString() => $"{Path.GetFileName(ProjectFile)}({TargetFramework})";
    }

    private sealed class CompilationTaskData
    {
        public readonly MSBuildProjectData ProjectData;
        public int TargetId;
        public string? CommandLineArguments;
        public CompilerCallKind? Kind;
        public int? CompileTaskId;
        public bool IsCSharp;

        public string ProjectFile => ProjectData.ProjectFile;
        public string? TargetFramework => ProjectData.TargetFramework;

        public CompilationTaskData(MSBuildProjectData projectData, int targetId)
        {
            ProjectData = projectData;
            TargetId = targetId;
        }

        public override string ToString() => $"{ProjectData} {TargetId}";

        internal CompilerCall? TryCreateCompilerCall(List<string> diagnosticList)
        {
            if (CommandLineArguments is null)
            {
                // An evaluation of the project that wasn't actually a compilation
                return null;
            }

            if (Kind is not { } kind)
            {
                diagnosticList.Add($"Project {ProjectFile} ({TargetFramework}): cannot find CoreCompile");
                return null;
            }

            var args = CommandLineParser.SplitCommandLineIntoArguments(CommandLineArguments, removeHashComments: true);
            var rawArgs = IsCSharp
                ? SkipCompilerExecutable(args, "csc.exe", "csc.dll").ToArray()
                : SkipCompilerExecutable(args, "vbc.exe", "vbc.dll").ToArray();
            if (rawArgs.Length == 0)
            {
                diagnosticList.Add($"Project {ProjectFile} ({TargetFramework}): bad argument list");
                return null;
            }

            return new CompilerCall(
                ProjectFile,
                kind,
                TargetFramework,
                isCSharp: IsCSharp,
                rawArgs,
                index: null);
        }
    }

    private sealed class MSBuildEvaluationData
    {
        public string ProjectFile;
        public string? TargetFramework;

        public MSBuildEvaluationData(string projectFile)
        {
            ProjectFile = projectFile;
        }
        public override string ToString() => $"{Path.GetFileName(ProjectFile)}({TargetFramework})";
    }
}
