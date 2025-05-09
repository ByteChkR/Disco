using System.Diagnostics;

using Disco.Core.Worker;

using Newtonsoft.Json.Linq;

namespace Disco.Core.Tasks;

public class DiscoTask : IComparable<DiscoTask>
{
    private static readonly Stopwatch _stopwatch = Stopwatch.StartNew();

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
    public long QueueTime => _stopwatch.ElapsedMilliseconds - EnqueuedAt;

#region IComparable<DiscoTask> Members

    public int CompareTo(DiscoTask? other)
    {
        if (other == null)
        {
            return 1;
        }

        if (Priority != other.Priority)
        {
            return Priority.CompareTo(other.Priority);
        }

        return Id.CompareTo(other.Id);
    }

#endregion

    public bool CanRunOn(DiscoWorkerCapabilities capabilities)
    {
        IEnumerable<string> required =
            new[] { $"disco://v1/capabilities/task/{TaskRunnerName}" }.Concat(AdditionalCapabilities);

        return required.All(r => capabilities.Capabilities.Contains(r));
    }
}