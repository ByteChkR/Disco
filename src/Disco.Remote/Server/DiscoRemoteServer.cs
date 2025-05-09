using System.Net;

using Disco.Core.Queue;
using Disco.Core.Tasks;
using Disco.Core.Worker;
using Disco.Remote.Common;

using Newtonsoft.Json;

namespace Disco.Remote.Server;

internal class DiscoRemoteServer : IDiscoRemoteServer
{
    private readonly HttpListener _listener;
    private readonly IDiscoTaskQueue _queue;

    public DiscoRemoteServer(IDiscoTaskQueue queue, params string[] prefixes)
    {
        _queue = queue;
        _listener = new HttpListener();

        foreach (string prefix in prefixes)
        {
            _listener.Prefixes.Add(prefix);
        }
    }

#region IDiscoRemoteServer Members

    public void Start(CancellationToken cancellationToken)
    {
        _ = Loop(cancellationToken);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Loop(cancellationToken);
    }

#endregion

    private async Task HandleRequest(HttpListenerContext context, CancellationToken cancellationToken)
    {
        //If GET /queue/isEmpty, return queue state
        if (context.Request.HttpMethod == "GET" && context.Request.Url!.AbsolutePath == "/queue/isEmpty")
        {
            bool isEmpty = await _queue.IsEmpty().ConfigureAwait(false);
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            using StreamWriter writer = new StreamWriter(context.Response.OutputStream);
            await writer.WriteAsync(isEmpty ? "true" : "false").ConfigureAwait(false);

            await writer.FlushAsync(cancellationToken)
                        .ConfigureAwait(false);
            context.Response.Close();

            return;
        }

        //If POST /queue/enqueue, enqueue the task
        if (context.Request.HttpMethod == "POST" && context.Request.Url!.AbsolutePath == "/queue/enqueue")
        {
            using StreamReader reader = new StreamReader(context.Request.InputStream);
            string body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            DiscoTaskDto task = JsonConvert.DeserializeObject<DiscoTaskDto>(body);
            //Enqueue the task
            await _queue.Enqueue(task.TaskRunnerName, task.Priority, task.Data, task.AdditionalCapabilities).ConfigureAwait(false);
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.Close();

            return;
        }

        //If POST /queue/submitResult, submit the result
        if (context.Request.HttpMethod == "POST" && context.Request.Url!.AbsolutePath == "/queue/submitResult")
        {
            using StreamReader reader = new StreamReader(context.Request.InputStream);
            string body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            DiscoResultDto result = JsonConvert.DeserializeObject<DiscoResultDto>(body);
            //Submit the result
            await _queue.SubmitResult(result.ToResult()).ConfigureAwait(false);
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.Close();

            return;
        }

        //If GET /queue/getResult/<taskId>, get the result
        if (context.Request.HttpMethod == "GET" && context.Request.Url!.AbsolutePath.StartsWith("/queue/getResult/"))
        {
            string id = context.Request.Url.AbsolutePath.Split('/')[3];
            DiscoResult? result = await _queue.TryGetResult(Guid.Parse(id)).ConfigureAwait(false);

            if (result != null)
            {
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                using StreamWriter writer = new StreamWriter(context.Response.OutputStream);
                await writer.WriteAsync(JsonConvert.SerializeObject(result.ToDto())).ConfigureAwait(false);
                await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                context.Response.StatusCode = (int)HttpStatusCode.NoContent;
            }

            context.Response.Close();

            return;
        }

        //If POST /queue/waitForTask, wait for the next task for the worker
        if (context.Request.HttpMethod == "POST" && context.Request.Url!.AbsolutePath == "/queue/waitForTask")
        {
            using StreamReader reader = new StreamReader(context.Request.InputStream);
            string body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            DiscoWorkerCapabilities capabilities = JsonConvert.DeserializeObject<DiscoWorkerCapabilities>(body);
            DiscoTask? task = await _queue.TryWaitForTask(capabilities, cancellationToken).ConfigureAwait(false);

            if (task == null)
            {
                context.Response.StatusCode = (int)HttpStatusCode.NoContent;
                context.Response.Close();

                return;
            }

            context.Response.StatusCode = (int)HttpStatusCode.OK;
            using StreamWriter writer = new StreamWriter(context.Response.OutputStream);
            await writer.WriteAsync(JsonConvert.SerializeObject(task.ToDto())).ConfigureAwait(false);
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            context.Response.Close();

            return;
        }

        //Respond with 404
        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
        using StreamWriter notFoundWriter = new StreamWriter(context.Response.OutputStream);
        await notFoundWriter.WriteAsync("Not Found: " + context.Request.Url!.AbsolutePath).ConfigureAwait(false);
        await notFoundWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
        context.Response.Close();
    }

    private async Task Loop(CancellationToken cancellationToken)
    {
        _listener.Start();

        while (!cancellationToken.IsCancellationRequested)
        {
            IAsyncResult request = _listener.BeginGetContext(null, null);

            while (!request.IsCompleted && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            HttpListenerContext context = _listener.EndGetContext(request);

            //Handle the request
            _ = HandleRequest(context, cancellationToken);
        }
    }
}