using Parallel.Compression.Cli.Ioc;
using Parallel.Compression.Cli.Output;
using Parallel.Compression.Func;
using Parallel.Compression.Logging;

namespace Parallel.Compression.Cli.Commands
{
    internal class HelpCommand : ICommand
    {
        public Result Execute(ILog log, Dependencies dependencies)
        {
            Print.Info(Messages.Help());
            return Result.Successful();
        }
    }
}