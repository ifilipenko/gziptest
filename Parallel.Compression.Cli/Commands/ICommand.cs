using Parallel.Compression.Cli.Ioc;
using Parallel.Compression.Func;
using Parallel.Compression.Logging;

namespace Parallel.Compression.Cli.Commands
{
    internal interface ICommand
    {
        Result Execute(ILog log, Dependencies dependencies);
    }
}