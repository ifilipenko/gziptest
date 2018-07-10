using System;
using Parallel.Compression.Cli.Ioc;
using Parallel.Compression.Cli.Output;
using Parallel.Compression.Cli.ParamsParsing;
using Parallel.Compression.Logging;

namespace Parallel.Compression.Cli
{
    public static class EntryPoint
    {
#if DEBUG
        private static readonly ILog Log = new ConsoleLog();
#else
        private static readonly ILog Log = new StubLog();
#endif

        public static int Run(string[] args)
        {
            try
            {
                var dependencies = new Dependencies(Log);
                var (command, parametersError) = CommandParser.Parse(args);
                if (parametersError != null)
                {
                    Print.Error(parametersError);
                    Print.Info(Messages.Help());
                    return -1;
                }

                var result = command.Execute(Log, dependencies);
                if (result.IsSuccessful)
                    return 0;
                
                Print.Error(result.ErrorMessage);
                return -1;
            }
            catch (Exception ex)
            {
                Print.Error(Messages.UnexpectedException(ex));
                return -1;
            }
        }
    }
}