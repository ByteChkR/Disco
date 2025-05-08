using Disco.Core.Queue;
using Disco.Core.Tasks;
using ewu.trace;
using ewu.trace.Data;
using Newtonsoft.Json.Linq;

namespace Disco.Core.Worker;

public class DiscoWorker
{
    private readonly IDiscoTaskQueue _queue;
    private readonly List<DiscoTaskRunner> _taskRunners = new();

    public DiscoWorker(DiscoWorkerInfo workerInfo, IDiscoTaskQueue queue)
    {
        WorkerInfo = workerInfo;
        _queue = queue;
    }

    public bool IsIdle { get; private set; }
    public bool IsStopped { get; private set; }
    public bool IsStopping { get; private set; }

    public DiscoWorkerInfo WorkerInfo { get; }
    public DiscoWorkerCapabilities Capabilities => new(WorkerInfo, _taskRunners.Select(x => x.Name).ToArray());

    public DiscoWorker AddRunner(params IEnumerable<DiscoTaskRunner> runner)
    {
        _taskRunners.AddRange(runner);
        return this;
    }

    public DiscoWorker AddRunner<T>() where T : DiscoTaskRunner, new()
    {
        var runner = new T();
        _taskRunners.Add(runner);
        return this;
    }

    private ITraceSession CreateSession(DiscoTask task)
    {
        return new TraceSession($"{task.TaskRunnerName}(on {WorkerInfo.Name}) - {task.Id}")
            .AssociateEntity("TaskId", task.Id)
            .AssociateEntity("WorkerId", WorkerInfo.Id)
            .AddData("WorkerName", WorkerInfo.Name)
            .AddData("TaskRunnerName", task.TaskRunnerName)
            .AddData("TaskData", task.Data)
            .ConfigureScopeTimingLogs()
            .ConfigureDefaultLogFormatter(Console.WriteLine);
    }


    public void GracefulStop()
    {
        IsStopping = true;
    }

    public async Task Run(CancellationToken cancellationToken)
    {
        IsStopped = false;
        IsStopping = false;
        try
        {
            while (!cancellationToken.IsCancellationRequested && !IsStopping)
            {
                IsIdle = true;
                var task = await _queue.WaitForTask(Capabilities, cancellationToken);
                IsIdle = false;
                JToken data;
                TraceSessionData? traceSessionData = null;
                await using (var session = CreateSession(task)
                                 .AddSessionFinishedHandler((_, d) => traceSessionData = d))
                {
                    using var scope = session.CreateScope([$"TaskId({task.Id}))", $"Priority({task.Priority})"]);
                    try
                    {
                        var runner = _taskRunners.FirstOrDefault(x => x.Name == task.TaskRunnerName);
                        if (runner == null)
                            throw new InvalidOperationException($"No task runner found for {task.TaskRunnerName}");

                        data = await runner.ExecuteAsync(task, new DiscoContext(session));
                    }
                    catch (Exception e)
                    {
                        data = JToken.FromObject(e);
                    }
                }

                if (traceSessionData == null) throw new InvalidOperationException("Trace session data is null");

                var result = new DiscoResult(task.Id, false, data, traceSessionData);

                await _queue.SubmitResult(result);
            }
        }
        finally
        {
            IsStopped = true;
            IsStopping = false;
            IsIdle = false;
        }
    }
    
}