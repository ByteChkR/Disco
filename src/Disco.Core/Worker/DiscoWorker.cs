using Disco.Core.Queue;
using Disco.Core.Tasks;
using ewu.trace;
using ewu.trace.Data;
using Newtonsoft.Json.Linq;

namespace Disco.Core.Worker;

public class DiscoWorker
{
    private readonly IDiscoTaskQueue _queue;
    private readonly string[] _capabilities;
    private readonly List<DiscoRunnerRegistration> _taskRunners = new();

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
    public DiscoWorkerCapabilities Capabilities => new(WorkerInfo, _capabilities.Concat(_taskRunners.Select(x => $"disco://v1/capabilities/task/{x.DiscoTaskType.Name}")).ToArray());

    public DiscoWorker AddRunner(params IEnumerable<DiscoRunnerRegistration> runner)
    {
        _taskRunners.AddRange(runner);
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

    protected virtual Task<JToken> RunTask(DiscoContext context,
                                          DiscoTask task,
                                          DiscoRunnerRegistration registration)
    {
        var runner = registration.DiscoTaskRunnerFactory?.Invoke();
        if(runner == null)
            throw new InvalidOperationException($"No task runner found for {task.TaskRunnerName}");
        return runner.ExecuteAsync(task, context);
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
                var task = await _queue.WaitForTask(Capabilities, cancellationToken).ConfigureAwait(false);
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
                    var registration = _taskRunners.FirstOrDefault(x => x.DiscoTaskType.Name == task.TaskRunnerName);
                    if (registration == null)
                        throw new InvalidOperationException($"No task runner found for {task.TaskRunnerName}");

                    data = await RunTask(new DiscoContext(session), task, registration).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    data = JToken.FromObject(e);
                    isError = true;
                }
                scope.Dispose();
                session.Dispose();

                if (traceSessionData == null) throw new InvalidOperationException("Trace session data is null");

                var result = new DiscoResult(task.Id, isError, data, traceSessionData);

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