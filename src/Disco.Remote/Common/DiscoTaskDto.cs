using Newtonsoft.Json.Linq;

namespace Disco.Remote.Common;

internal struct DiscoTaskDto
{
    public Guid Id { get; set; }
    public string TaskRunnerName { get; set; }
    public JToken Data { get; set; }
    public int Priority { get; set; }
    public string[] AdditionalCapabilities { get; set; }
}