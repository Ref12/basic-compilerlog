using System.Collections;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;

namespace Basic.CompilerLog.Util;

internal partial class MSBuildBinaryLogCompilerCallCollector : BinaryLogCompilerCallCollectorBase
{
    public MSBuildBinaryLogCompilerCallCollector(List<string> diagnosticList, Func<CompilerCall, bool> predicate)
        : base(diagnosticList, predicate)
    {
    }

    protected override void ReplayEvents(Stream stream)
    {
        if (stream is FileStream fs)
        {
            var eventSource = new BinaryLogReplayEventSource();

            eventSource.AnyEventRaised += EventSource_AnyEventRaised;

            eventSource.Replay(fs.Name);
        }
        else
        {
            throw new InvalidOperationException($"Only files are supported when using {nameof(MSBuildBinaryLogCompilerCallCollector)}");
        }
    }

    private void EventSource_AnyEventRaised(object sender, BuildEventArgs e)
    {
        HandleEvent(e);
    }
}
