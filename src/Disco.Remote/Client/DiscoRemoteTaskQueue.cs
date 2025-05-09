using System.Net;

using Disco.Core.Queue;
using Disco.Core.Tasks;
using Disco.Core.Worker;
using Disco.Remote.Common;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Disco.Remote.Client;

internal class DiscoRemoteTaskQueue : IDiscoTaskQueue
{
    private readonly HttpClient _client;

    public DiscoRemoteTaskQueue(HttpClient client)
    {
        _client = client;
    }

#region IDiscoTaskQueue Members

    public async Task<bool> IsEmpty()
    {
        HttpResponseMessage response = await _client.GetAsync("/queue/isEmpty").ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            return JsonConvert.DeserializeObject<bool>(content);
        }

        throw new Exception("Error while checking if queue is empty");
    }

    public async Task<DiscoTask> WaitForTask(DiscoWorkerCapabilities capabilities, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            DiscoTask? result = await TryWaitForTask(capabilities, cancellationToken).ConfigureAwait(false);

            if (result != null)
            {
                return result;
            }

            Thread.Sleep(100);
        }

        throw new OperationCanceledException("Task queue was cancelled");
    }

    public async Task<DiscoTask?> TryWaitForTask(DiscoWorkerCapabilities capabilities,
                                                 CancellationToken cancellationToken)
    {
        // POST /queue/waitForTask
        // => Body is capabilities
        // => Returns 200 or 201
        // => Returns DiscoTask if 200
        // => Returns 201 if no task is available(retry later)
        HttpResponseMessage response = await _client.PostAsync("/queue/waitForTask",
                                                               new StringContent(JsonConvert
                                                                       .SerializeObject(capabilities)
                                                                   ),
                                                               cancellationToken
                                                              ).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            string content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            DiscoTaskDto result = JsonConvert.DeserializeObject<DiscoTaskDto>(content);

            return result.ToTask();
        }

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return null;
        }

        throw new Exception("Error while waiting for task");
    }

    public async Task<Guid> Enqueue(Guid id, string taskRunnerName,
                                    int priority,
                                    JToken data,
                                    params string[] additionalCapabilities)
    {
        // POST /queue/enqueue/<id>
        // => Body is DiscoTask
        // => Returns 200
        DiscoTask task = new DiscoTask(id, taskRunnerName, data, priority, additionalCapabilities);

        HttpResponseMessage response =
            await _client.PostAsync("/queue/enqueue", new StringContent(JsonConvert.SerializeObject(task.ToDto()))).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            return task.Id;
        }

        throw new Exception("Error while enqueuing task");
    }

    public async Task SubmitResult(DiscoResult result)
    {
        // POST /queue/submitResult/<taskId>
        // => Body is DiscoResult
        // => Returns 200
        HttpResponseMessage response =
            await _client.PostAsync("/queue/submitResult",
                                    new StringContent(JsonConvert.SerializeObject(result.ToDto()))
                                   ).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            return;
        }

        throw new Exception("Error while submitting result");
    }

    public async Task<DiscoResult?> TryGetResult(Guid taskId)
    {
        HttpResponseMessage response = await _client.GetAsync("/queue/getResult/" + taskId).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            DiscoResultDto result = JsonConvert.DeserializeObject<DiscoResultDto>(content);

            return result.ToResult();
        }

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return null;
        }

        throw new Exception("Error while getting result");
    }

    public async Task<DiscoResult> GetResult(Guid taskId, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            DiscoResult? result = await TryGetResult(taskId).ConfigureAwait(false);

            if (result != null)
            {
                return result;
            }

            Thread.Sleep(100);
        }

        throw new OperationCanceledException("Task queue was cancelled");
    }

#endregion
}