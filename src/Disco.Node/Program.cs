using Disco.Core;
using Disco.Remote;
using Disco.Test.TaskTypes;

namespace Disco.Node;

internal class Program
{
    private static async Task Main(string[] args)
    {
        string prefix = "http://localhost:4578/";

        //Configure a Node that will process the tasks
        DiscoNode node = DiscoNode.FromFile(args[0], i => DiscoRemote.CreateClient(prefix))
                                  //Add the WaitForTask Implementation.
                                  //This makes this node capable of accepting tasks of this type
                                  .AddRunner<WaitForTask>()
                                  .AddRunner<AddTask>()
                                  .AddRunner<ExecuteGraphTask>();

        //Start the node
        await node.Run();
    }
}