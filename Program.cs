using System;
using System.Threading;
using Tumba.CanLindaControl.Model;
using Tumba.CanLindaControl.Services;

namespace Tumba.CanLindaControl
{
    public class Program
    {
        public const string USAGE = @"Usage:
Before using this utility you must run Linda-qt.exe with the following command:
Linda-qt.exe -server=1 -rpcuser=user -rpcpassword=password -rpcallowip=127.0.0.1 -rpcport=15715

-rpcuser and -rpcpassword should be changed.

Tumba can Linda control command line methods:
coincontrol {rpcuser} {rpcpassword} {frequencyInMilliseconds}";

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

            Method method;
            if (!Enum.TryParse<Method>(args[0], true, out method))
            {
                Console.WriteLine("Specified method not recognized!");
                Environment.Exit(-1);
            }

            using (ManualResetEvent wait = new ManualResetEvent(false))
            {
                ConsoleMessageHandlingService messagHandler = new ConsoleMessageHandlingService(() =>
                {
                    wait.Set();
                });

                string errorMessage;
                switch (method)
                {
                    case Method.CoinControl:
                    default:
                    {
                        CoinControlService service = new CoinControlService(messagHandler);
                        if (!service.TryParseArgs(args, out errorMessage))
                        {
                            Console.WriteLine(errorMessage);
                            Environment.Exit(-2);
                        }
                        service.Start();
                        break;
                    }
                }

                wait.WaitOne();
            }
        }
    }
}
