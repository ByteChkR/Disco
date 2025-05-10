using Disco.Core.Queue;
using Disco.Core.Tasks;
using Disco.Remote.Client;
using Disco.Remote.Common;
using Disco.Remote.Server;

namespace Disco.Remote;

public static class DiscoRemote
{
    public static IDiscoRemoteServer CreateServer(IDiscoTaskQueue queue, params string[] prefixes) => new DiscoRemoteServer(queue, prefixes);

    public static IDiscoTaskQueue CreateClient(string baseUrl)
    {
        var client = new HttpClient();
        client.BaseAddress = new Uri(baseUrl);
        return new DiscoRemoteTaskQueue(client);
    }

    internal static DiscoResultDto ToDto(this DiscoResult result) => new DiscoResultDto
    {
        TaskId = result.TaskId,
        IsError = result.IsError,
        Data = result.Data,
        TraceSessionData = result.TraceSessionData
    };
    
    internal static DiscoTaskDto ToDto(this DiscoTask task) => new DiscoTaskDto
    {
        Id = task.Id,
        TaskRunnerName = task.TaskRunnerName,
        Data = task.Data,
        Priority = task.Priority,
        AdditionalCapabilities = task.AdditionalCapabilities
    };
    
    internal static DiscoResult ToResult(this DiscoResultDto result) => new DiscoResult(result.TaskId, result.IsError, result.Data, result.TraceSessionData);
    
    internal static DiscoTask ToTask(this DiscoTaskDto task) => new DiscoTask(task.Id, task.TaskRunnerName, task.Data, task.Priority, task.AdditionalCapabilities);
}