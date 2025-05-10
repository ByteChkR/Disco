using System.Net;

using Disco.Core.Queue;
using Disco.Core.Tasks;
using Disco.Core.Worker;
using Disco.Remote.Common;

using ewu.trace;
using ewu.trace.Data;

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
        _ = Loop(cancellationToken).ConfigureAwait(false);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await Loop(cancellationToken).ConfigureAwait(false);
    }

#endregion

    private ITraceSession CreateSession(HttpListenerContext context)
    {
        return new TraceSession($"{context.Request.RemoteEndPoint.Address} - {context.Request.HttpMethod} - {context.Request.Url!.AbsoluteUri}")
               .AddCategory("SessionType", "RequestHandler")
               .AddData("Method", context.Request.HttpMethod)
               .AddData("Url", context.Request.Url.AbsoluteUri)
               .AddData("RemoteEndPoint", context.Request.RemoteEndPoint?.Address.ToString()!)
               .ConfigureDefaultLogFormatter(Console.WriteLine)
               .ConfigureScopeTimingLogs();
    }
    private async Task HandleRequest(HttpListenerContext context, CancellationToken cancellationToken)
    {
        TraceSessionData? sessionData = null;
        ITraceSession session = CreateSession(context)
            .AddSessionFinishedHandler((_, d) => sessionData = d);
        using (ITraceScope scope = session.CreateScope([context.Request.HttpMethod, context.Request.Url!.AbsolutePath]))
        {
            //If GET /queue/isEmpty, return queue state
            if (context.Request.HttpMethod == "GET" && context.Request.Url.AbsolutePath == "/queue/isEmpty")
            {
                bool isEmpty = await _queue.IsEmpty().ConfigureAwait(false);
                scope.Debug($"Queue is Empty: {isEmpty}");
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                using StreamWriter writer = new StreamWriter(context.Response.OutputStream);
                await writer.WriteAsync(isEmpty ? "true" : "false").ConfigureAwait(false);
                await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                context.Response.Close();
            }

            //If POST /queue/enqueue, enqueue the task
            else if (context.Request.HttpMethod == "POST" && context.Request.Url.AbsolutePath == "/queue/enqueue")
            {
                using StreamReader reader = new StreamReader(context.Request.InputStream);
                string body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                DiscoTaskDto task = JsonConvert.DeserializeObject<DiscoTaskDto>(body);
                
                scope.Info($"Enqueueing task: {task}");
                session
                    .AssociateEntity("Task", task.Id)
                    .AddData("Body", JsonConvert.SerializeObject(task, Formatting.Indented));
                
                //Enqueue the task
                await _queue.Enqueue(task.TaskRunnerName, task.Priority, task.Data, task.AdditionalCapabilities).ConfigureAwait(false);
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.Close();
            }

            //If POST /queue/submitResult, submit the result
            else if (context.Request.HttpMethod == "POST" && context.Request.Url.AbsolutePath == "/queue/submitResult")
            {
                using StreamReader reader = new StreamReader(context.Request.InputStream);
                string body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                DiscoResultDto result = JsonConvert.DeserializeObject<DiscoResultDto>(body);
                scope.Info($"Submitting result: {result}");
                session.AssociateEntity("Task", result.TaskId)
                       .AddData("Body", JsonConvert.SerializeObject(result, Formatting.Indented));
                //Submit the result
                await _queue.SubmitResult(result.ToResult()).ConfigureAwait(false);
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.Close();
            }

            //If GET /queue/getResult/<taskId>, get the result
            else if (context.Request.HttpMethod == "GET" && context.Request.Url.AbsolutePath.StartsWith("/queue/getResult/"))
            {
                var id = Guid.Parse(context.Request.Url.AbsolutePath.Split('/')[3]);
                DiscoResult? result = await _queue.TryGetResult(id).ConfigureAwait(false);
                session.AssociateEntity("Task", id);
                if (result != null)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    scope.Info($"Result found: {result}");
                    session.AddData("Result", JsonConvert.SerializeObject(result, Formatting.Indented));
                    using StreamWriter writer = new StreamWriter(context.Response.OutputStream);
                    await writer.WriteAsync(JsonConvert.SerializeObject(result.ToDto())).ConfigureAwait(false);
                    await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NoContent;
                }
                context.Response.Close();
            }

            //If POST /queue/waitForTask, wait for the next task for the worker
            else if (context.Request.HttpMethod == "POST" && context.Request.Url.AbsolutePath == "/queue/waitForTask")
            {
                using StreamReader reader = new StreamReader(context.Request.InputStream);
                string body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                DiscoWorkerCapabilities capabilities = JsonConvert.DeserializeObject<DiscoWorkerCapabilities>(body);
                DiscoTask? task = await _queue.TryWaitForTask(capabilities, cancellationToken).ConfigureAwait(false);
                session.AssociateEntity("Worker", capabilities.WorkerInfo.Id)
                       .AddCategory("Worker", capabilities.WorkerInfo.Name)
                       .AddData("Body", JsonConvert.SerializeObject(capabilities, Formatting.Indented));
                if (task == null)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NoContent;
                    context.Response.Close();
                }
                else
                {
                    var dto = task.ToDto();
                    scope.Info($"Task found: {task}");
                    session.AssociateEntity("Task", task.Id)
                           .AddData("Task", JsonConvert.SerializeObject(dto, Formatting.Indented));
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    using StreamWriter writer = new StreamWriter(context.Response.OutputStream);
                    await writer.WriteAsync(JsonConvert.SerializeObject(dto)).ConfigureAwait(false);
                    await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                    context.Response.Close();
                }
            }
            else
            {
                //Respond with 404
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                using StreamWriter notFoundWriter = new StreamWriter(context.Response.OutputStream);
                await notFoundWriter.WriteAsync("Not Found: " + context.Request.Url.AbsolutePath).ConfigureAwait(false);
                await notFoundWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
                context.Response.Close();
            }

            session.AddCategory("StatusCode", context.Response.StatusCode.ToString());
        }
        
        await session.DisposeAsync().ConfigureAwait(false);
        if(sessionData==null)throw new Exception("Session data is null");
        
    }

    private async Task Loop(CancellationToken cancellationToken)
    {
        _listener.Start();
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context = await _listener.GetContextAsync().ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested) break;

            //Handle the request
            await HandleRequest(context, cancellationToken).ConfigureAwait(false);
        }
    }
}