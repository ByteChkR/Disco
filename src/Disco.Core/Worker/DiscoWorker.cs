using Disco.Core.Queue;
using Disco.Core.Tasks;

using ewu.trace;
using ewu.trace.Data;

using Newtonsoft.Json.Linq;

namespace Disco.Core.Worker;

public class DiscoWorker
{
    private readonly string[] _capabilities;
    private readonly IDiscoTaskQueue _queue;
    private readonly List<DiscoTaskRunner> _taskRunners = new List<DiscoTaskRunner>();

    public DiscoWorker(DiscoWorkerInfo workerInfo, IDiscoTaskQueue queue, string[] capabilities)
    {
        WorkerInfo = workerInfo;
        _queue = queue;
        _capabilities = capabilities;
    }

    public bool IsIdle { get; private set; }
    public bool IsStopped { get; private set; }
    public bool IsStopping { get; private set; }

    public DiscoWorkerInfo WorkerInfo { get; }

    public DiscoWorkerCapabilities Capabilities => new DiscoWorkerCapabilities(WorkerInfo,
                                                                               _capabilities
                                                                                   .Concat(_taskRunners.Select(x =>
                                                                                                   $"disco://v1/capabilities/task/{x.Name}"
                                                                                               )
                                                                                       )
                                                                                   .ToArray()
                                                                              );

    public DiscoWorker AddRunner(params IEnumerable<DiscoTaskRunner> runner)
    {
        _taskRunners.AddRange(runner);

        return this;
    }

    public DiscoWorker AddRunner<T>() where T : DiscoTaskRunner, new()
    {
        T runner = new T();
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
                DiscoTask task = await _queue.WaitForTask(Capabilities, cancellationToken).ConfigureAwait(false);
                IsIdle = false;
                JToken data;
                TraceSessionData? traceSessionData = null;
                bool isError = false;

                ITraceSession session = CreateSession(task)
                    .AddSessionFinishedHandler((_, d) => traceSessionData = d);

                ITraceScope scope =
                    session.CreateScope([$"TaskId({task.Id}))", $"Priority({task.Priority})"]);

                try
                {
                    DiscoTaskRunner? runner = _taskRunners.FirstOrDefault(x => x.Name == task.TaskRunnerName);

                    if (runner == null)
                    {
                        throw new InvalidOperationException($"No task runner found for {task.TaskRunnerName}");
                    }

                    data = await runner.ExecuteAsync(task, new DiscoContext(session)).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    data = JToken.FromObject(e);
                    isError = true;
                }

                scope.Dispose();

                await session.DisposeAsync()
                             .ConfigureAwait(false);

                if (traceSessionData == null)
                {
                    throw new InvalidOperationException("Trace session data is null");
                }

                DiscoResult result = new DiscoResult(task.Id, isError, data, traceSessionData);

                await _queue.SubmitResult(result).ConfigureAwait(false);
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