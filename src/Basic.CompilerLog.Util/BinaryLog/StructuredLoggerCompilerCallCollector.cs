using System.Collections;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging.StructuredLogger;

namespace Basic.CompilerLog.Util;

internal partial class StructuredLoggerCompilerCallCollector : BinaryLogCompilerCallCollectorBase
{
    public StructuredLoggerCompilerCallCollector(List<string> diagnosticList, Func<CompilerCall, bool> predicate)
        : base(diagnosticList, predicate)
    {
    }

    protected override void ReplayEvents(Stream stream)
    {
        var records = BinaryLog.ReadRecords(stream);
        foreach (var record in records)
        {
            HandleEvent(record.Args);
        }
    }
}
