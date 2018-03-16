using System;
using Tumba.CanLindaControl.Model;
using Tumba.CanLindaControl.Services;

namespace Tumba.CanLindaControl
{
    public class Program
    {
        public const string USAGE = @"Usage:
Before using this utility you must run Linda-qt.exe with the following command:
Linda-qt.exe -server=1 -rpcuser=user -rpcpassword=password -rpcallowip=127.0.0.1 -rpcport=33821

-rpcuser and -rpcpassword should be changed.

Tumba can Linda control command line verb examples:
coincontrol {rpcuser} {rpcpassword} {accountToCoinControl} {frequencyInMilliseconds}";

        public static void Main(string[] args)
        {
            if (args.Length < 1 || 
                args[0].Equals("h", StringComparison.InvariantCultureIgnoreCase) ||
                args[0].Equals("help", StringComparison.InvariantCultureIgnoreCase) ||
                args[0].Equals("?"))
            {
                Console.WriteLine(USAGE);
                return;
            }

            Verb verb;
            if (!Enum.TryParse<Verb>(args[0], true, out verb))
            {
                Console.WriteLine("Specified verb not recognized!");
                Environment.Exit(-1);
            }

            ConsoleMessageHandlingService messageService = new ConsoleMessageHandlingService();

            // TODO: Add a verb for handling master node earnings.
            IRunnable runnable;
            switch (verb)
            {
                case Verb.CoinControl:
                default:
                {
                    runnable = new CoinControlService(messageService);
                    break;
                }
            }

            string errorMessage;
            if (!runnable.Run(args, out errorMessage))
            {
                Console.WriteLine("Run failed!  See error: {0}", errorMessage);
                Environment.Exit(-2);
            }
        }
    }
}
