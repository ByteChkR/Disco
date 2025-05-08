using System.Diagnostics;
using Disco.Core.Worker;
using Newtonsoft.Json.Linq;

namespace Disco.Core.Tasks;

public class DiscoTask : IComparable<DiscoTask>
{
    public DiscoTask(Guid id, string taskRunnerName, JToken data, int priority, string[] additionalCapabilities)
    {
        Id = id;
        TaskRunnerName = taskRunnerName;
        Data = data;
        Priority = priority;
        AdditionalCapabilities = additionalCapabilities;
    }

    public Guid Id { get; }
    public string TaskRunnerName { get; }
    public JToken Data { get; }
    public int Priority { get; }
    public string[] AdditionalCapabilities { get; }
    public long EnqueuedAt { get; set; }
    public long QueueTime => _stopwatch.ElapsedMilliseconds-EnqueuedAt;
    private static readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    public bool CanRunOn(DiscoWorkerCapabilities capabilities)
    {
        var required = new[]{TaskRunnerName}.Concat(AdditionalCapabilities);
        return required.All(r => capabilities.Capabilities.Contains(r));
    }

    public int CompareTo(DiscoTask? other)
    {
        if (other == null) return 1;
        if (Priority != other.Priority) return Priority.CompareTo(other.Priority);
        return Id.CompareTo(other.Id);
    }
}