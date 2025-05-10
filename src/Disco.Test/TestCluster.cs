using System.Diagnostics;

using Disco.Core;
using Disco.Core.Queue;
using Disco.Core.Tasks;
using Disco.Remote;
using Disco.Test.TaskTypes;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Disco.Test;

internal static class TestCluster
{
    private static async Task FillQueue(IDiscoTaskQueue queue, int count)
    {
        Random rnd = new Random();

        for (int i = 0; i < count; i++)
        {
            WaitForArgs a = new WaitForArgs { Delay = rnd.Next(100, 1000) };
            //Wait for random time with different priorities
            Console.WriteLine($"Enqueueing Task {i}");
            await queue.Enqueue(nameof(WaitForTask), rnd.Next(1, 3), JToken.FromObject(a), "disco://pool/default");
        }
    }

    public static async Task Run(string[] args)
    {
        string prefix = "http://localhost:4578/";

        await Parallel.ForEachAsync(Enumerable.Repeat(0, 20),
                                    CancellationToken.None,
                                    async (i, token) =>
                                    {
                                        IDiscoTaskQueue client = DiscoRemote.CreateClient(prefix);
                                        await FillQueue(client, 100);
                                    }
                                   );

        IDiscoTaskQueue client = DiscoRemote.CreateClient(prefix);
        Stopwatch sw = Stopwatch.StartNew();

        DiscoResult result = await client.EnqueueAndWait(100,CancellationToken.None,
                                                         nameof(AddTask),
                                                         0,
                                                         JToken.FromObject(new AddArgs { A = 12312, B = 1231231 }),
                                                         "disco://pool/high"
                                                        );
        sw.Stop();

        if (result.IsError)
        {
            throw new InvalidOperationException("Error in task: " + result.Data.ToString(Formatting.Indented));
        }

        Console.WriteLine($"[{sw.ElapsedMilliseconds}ms] The Result is: " + result.Data.ToString(Formatting.Indented));

        //Adding a task with requirement "disco://pool/high" would lead to a near instantaneous result
        //as the task would be scheduled on one of the workers in the HighPriority pool
    }
}