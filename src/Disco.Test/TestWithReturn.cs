using Disco.Core;
using Disco.Core.Queue;
using Disco.Core.Tasks;
using Disco.Test.TaskTypes;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Disco.Test;

internal static class TestWithReturn
{
    public static async Task Run(string[] args)
    {
        //Create a Task Queue that is local to this process
        DiscoLocalTaskQueue queue = new DiscoLocalTaskQueue();

        //Configure a Node that will process the tasks
        DiscoNode node = new DiscoNode("Node", 100, 100, i => queue, null)
            //Add the WaitForTask Implementation.
            //This makes this node capable of accepting tasks of this type
            .AddRunner<AddTask>(() => new AddTask());

        //Start the node
        node.Run();

        AddArgs a = new AddArgs { A = 1, B = 2 };
        DiscoResult result = await queue.EnqueueAndWait(100,CancellationToken.None, nameof(AddTask), 1, JToken.FromObject(a));

        if (result.IsError)
        {
            throw new InvalidOperationException("Error in task: " + result.Data.ToString(Formatting.Indented));
        }

        Console.WriteLine("The Result is: " + result.Data.ToString(Formatting.Indented));

        //Wait for all threads to be idle and the queue beeing empty.
        await node.StopOnIdle();
    }
}