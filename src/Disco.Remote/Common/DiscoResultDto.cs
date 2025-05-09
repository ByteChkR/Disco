using ewu.trace.Data;

using Newtonsoft.Json.Linq;

namespace Disco.Remote.Common;

internal struct DiscoResultDto
{
    public Guid TaskId { get; set; }
    public bool IsError { get; set; }
    public JToken Data { get; set; }
    public TraceSessionData TraceSessionData { get; set; }
}