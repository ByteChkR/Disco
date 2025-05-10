using ewu.trace.Data;
using Newtonsoft.Json.Linq;

namespace Disco.Core.Tasks;

public class DiscoResult
{
    public DiscoResult(Guid taskId, bool isError, JToken data, TraceSessionData traceSessionData)
    {
        TaskId = taskId;
        IsError = isError;
        Data = data;
        TraceSessionData = traceSessionData;
    }

    public Guid TaskId { get; }
    public bool IsError { get; }
    public JToken Data { get; }
    public TraceSessionData TraceSessionData { get; }
}