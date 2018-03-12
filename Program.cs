using System;

namespace TumbaCanLindaControl
{
    public class Program
    {
        public const string USAGE = @"Usage:
coincontrol {address} {privateKey} {frequencyInMilliseconds}";

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
        }
    }
}
