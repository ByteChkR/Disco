using System.Net;
using Disco.Core.Queue;
using Disco.Core.Worker;
using Disco.Remote.Common;
using Newtonsoft.Json;

namespace Disco.Remote.Server;

internal class DiscoRemoteServer : IDiscoRemoteServer
{
    private readonly IDiscoTaskQueue _queue;
    private readonly HttpListener _listener;

    public DiscoRemoteServer(IDiscoTaskQueue queue, params string[] prefixes)
    {
        _queue = queue;
        _listener = new HttpListener();
        foreach (var prefix in prefixes)
        {
            _listener.Prefixes.Add(prefix);
        }
    }

    private async Task HandleRequest(HttpListenerContext context, CancellationToken cancellationToken)
    {
        //If GET /queue/isEmpty, return queue state
        if (context.Request.HttpMethod == "GET" && context.Request.Url.AbsolutePath == "/queue/isEmpty")
        {
            var isEmpty = await _queue.IsEmpty();
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            await using var writer = new StreamWriter(context.Response.OutputStream);
            await writer.WriteAsync(isEmpty ? "true" : "false");
            await writer.FlushAsync(cancellationToken);
            context.Response.Close();
            return;
        }
        
        //If POST /queue/enqueue, enqueue the task
        if (context.Request.HttpMethod == "POST" && context.Request.Url.AbsolutePath == "/queue/enqueue")
        {
            using var reader = new StreamReader(context.Request.InputStream);
            var body = await reader.ReadToEndAsync(cancellationToken);
            var task = JsonConvert.DeserializeObject<DiscoTaskDto>(body);
            //Enqueue the task
            await _queue.Enqueue(task.TaskRunnerName, task.Priority, task.Data, task.AdditionalCapabilities);
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.Close();
            return;
        }
        
        //If POST /queue/submitResult, submit the result
        if (context.Request.HttpMethod == "POST" && context.Request.Url.AbsolutePath == "/queue/submitResult")
        {
            using var reader = new StreamReader(context.Request.InputStream);
            var body = await reader.ReadToEndAsync(cancellationToken);
            var result = JsonConvert.DeserializeObject<DiscoResultDto>(body);
            //Submit the result
            await _queue.SubmitResult(result.ToResult());
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.Close();
            return;
        }
        
        //If GET /queue/getResult/<taskId>, get the result
        if (context.Request.HttpMethod == "GET" && context.Request.Url.AbsolutePath.StartsWith("/queue/getResult/"))
        {
            var id = context.Request.Url.AbsolutePath.Split('/')[3];
            var result = await _queue.TryGetResult(Guid.Parse(id));
            if (result != null)
            {
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                await using var writer = new StreamWriter(context.Response.OutputStream);
                await writer.WriteAsync(JsonConvert.SerializeObject(result.ToDto()));
                await writer.FlushAsync(cancellationToken);
            }
            else
            {
                context.Response.StatusCode = (int)HttpStatusCode.NoContent;
            }
            context.Response.Close();
            return;
        }
        
        //If POST /queue/waitForTask, wait for the next task for the worker
        if (context.Request.HttpMethod == "POST" && context.Request.Url.AbsolutePath == "/queue/waitForTask")
        {
            using var reader = new StreamReader(context.Request.InputStream);
            var body = await reader.ReadToEndAsync(cancellationToken);
            var capabilities = JsonConvert.DeserializeObject<DiscoWorkerCapabilities>(body);
            var task = await _queue.TryWaitForTask(capabilities, cancellationToken);
            if (task == null)
            {
                context.Response.StatusCode = (int)HttpStatusCode.NoContent;
                context.Response.Close();
                return;
            }
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            await using var writer = new StreamWriter(context.Response.OutputStream);
            await writer.WriteAsync(JsonConvert.SerializeObject(task.ToDto()));
            await writer.FlushAsync(cancellationToken);
            context.Response.Close();
            return;
        }
        
        
        //Respond with 404
        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
        await using var notFoundWriter = new StreamWriter(context.Response.OutputStream);
        await notFoundWriter.WriteAsync("Not Found: " + context.Request.Url.AbsolutePath);
        await notFoundWriter.FlushAsync(cancellationToken);
        context.Response.Close();
    }

    private async Task Loop(CancellationToken cancellationToken)
    {
        _listener.Start();
        while (!cancellationToken.IsCancellationRequested)
        {
            var request = _listener.BeginGetContext(null, null);
            while(!request.IsCompleted && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(100, cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested) break;
            var context = _listener.EndGetContext(request);
            
            //Handle the request
            _ = HandleRequest(context, cancellationToken);
        }
    }

    public void Start(CancellationToken cancellationToken)
    {
        _ = Loop(cancellationToken);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Loop(cancellationToken);
    }
}