using System.Collections;
using Microsoft.Build.Framework;

namespace Basic.CompilerLog.Util;

internal abstract partial class BinaryLogCompilerCallCollectorBase
{
    private readonly List<string> diagnosticList;
    private readonly Func<CompilerCall, bool> predicate;

    protected readonly List<CompilerCall> list = new();
    private readonly Dictionary<int, MSBuildProjectData> contextMap = new Dictionary<int, MSBuildProjectData>();
    private readonly Dictionary<int, MSBuildEvaluationData> evaluationMap = new Dictionary<int, MSBuildEvaluationData>();

    public BinaryLogCompilerCallCollectorBase(List<string> diagnosticList, Func<CompilerCall, bool> predicate)
    {
        this.diagnosticList = diagnosticList;
        this.predicate = predicate;
    }

    public List<CompilerCall> Collect(Stream stream)
    {
        ReplayEvents(stream);
        return list;
    }

    protected abstract void ReplayEvents(Stream stream);

    protected void HandleEvent(BuildEventArgs args)
    {
        if (args is not { BuildEventContext: { } context })
        {
            return;
        }

        switch (args)
        {
            case ProjectStartedEventArgs { ProjectFile: not null } e:
                {
                    var data = GetOrCreateData(context, e.ProjectFile);
                    data.EvaluationId = GetEvaluationId(e);
                    SetTargetFramework(ref data.TargetFramework, e.Properties);
                    break;
                }
            case ProjectFinishedEventArgs e:
                {
                    if (contextMap.TryGetValue(context.ProjectContextId, out var data))
                    {
                        if (string.IsNullOrEmpty(data.TargetFramework) &&
                            data.EvaluationId is { } evaluationId &&
                            evaluationMap.TryGetValue(evaluationId, out var evaluationData) &&
                            !string.IsNullOrEmpty(evaluationData.TargetFramework))
                        {
                            data.TargetFramework = evaluationData.TargetFramework;
                        }

                        foreach (var compilerCall in data.GetAllCompilerCalls(diagnosticList))
                        {
                            if (predicate(compilerCall))
                            {
                                list.Add(compilerCall);
                            }
                        }
                    }
                    break;
                }
            case ProjectEvaluationStartedEventArgs { ProjectFile: not null } e:
                {
                    var data = new MSBuildEvaluationData(e.ProjectFile);
                    evaluationMap[context.EvaluationId] = data;
                    break;
                }
            case ProjectEvaluationFinishedEventArgs e:
                {
                    if (evaluationMap.TryGetValue(context.EvaluationId, out var data))
                    {
                        SetTargetFramework(ref data.TargetFramework, e.Properties);
                    }
                    break;
                }
            case TargetStartedEventArgs e:
                {
                    var callKind = e.TargetName switch
                    {
                        "CoreCompile" => CompilerCallKind.Regular,
                        "CoreGenerateSatelliteAssemblies" => CompilerCallKind.Satellite,
                        _ => (CompilerCallKind?)null
                    };

                    if (callKind is { } ck &&
                        context.TargetId != BuildEventContext.InvalidTargetId &&
                        contextMap.TryGetValue(context.ProjectContextId, out var data))
                    {
                        data.GetOrCreateTaskData(context.TargetId).Kind = ck;
                    }

                    break;
                }
            case TaskStartedEventArgs e:
                {
                    if ((e.TaskName == "Csc" || e.TaskName == "Vbc") &&
                        context.TargetId != BuildEventContext.InvalidTargetId &&
                        contextMap.TryGetValue(context.ProjectContextId, out var data))
                    {
                        var taskData = data.GetOrCreateTaskData(context.TargetId);
                        taskData.IsCSharp = e.TaskName == "Csc";
                        taskData.CompileTaskId = context.TaskId;
                    }
                    break;
                }
            case TaskCommandLineEventArgs e:
                {
                    if (context.TargetId != BuildEventContext.InvalidTargetId &&
                        contextMap.TryGetValue(context.ProjectContextId, out var data) &&
                        data.TryGetTaskData(context.TargetId, out var taskData))
                    {
                        taskData.CommandLineArguments = e.CommandLine;
                    }

                    break;
                }
        }

        static int? GetEvaluationId(ProjectStartedEventArgs e)
        {
            if (e.BuildEventContext is { EvaluationId: > BuildEventContext.InvalidEvaluationId })
            {
                return e.BuildEventContext.EvaluationId;
            }

            if (e.ParentProjectBuildEventContext is { EvaluationId: > BuildEventContext.InvalidEvaluationId })
            {
                return e.ParentProjectBuildEventContext.EvaluationId;
            }

            return null;
        }

        MSBuildProjectData GetOrCreateData(BuildEventContext context, string projectFile)
        {
            if (!contextMap.TryGetValue(context.ProjectContextId, out var data))
            {
                data = new MSBuildProjectData(projectFile);
                contextMap[context.ProjectContextId] = data;
            }

            return data;
        }

        void SetTargetFramework(ref string? targetFramework, IEnumerable? rawProperties)
        {
            if (rawProperties is not IEnumerable<KeyValuePair<string, string>> properties)
            {
                return;
            }

            foreach (var property in properties)
            {
                switch (property.Key)
                {
                    case "TargetFramework":
                        targetFramework = property.Value;
                        break;
                    case "TargetFrameworks":
                        if (string.IsNullOrEmpty(targetFramework))
                        {
                            targetFramework = property.Value;
                        }
                        break;
                }
            }
        }
    }
}
