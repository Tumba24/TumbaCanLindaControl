using System;
using Tumba.CanLindaControl.Model.Linda.Requests;

namespace Tumba.CanLindaControl.Services
{
    public class ConsoleMessageHandlingService
    {
        public Action FailCallback { get; private set; }
        public ConsoleMessageHandlingService(Action failCallback)
        {
            FailCallback = failCallback;
        }

        public static void WriteMessageToConsole(string type, string message)
        {
            Console.WriteLine("[{0}] {1}", type, message);
        }

        public void Info(string message)
        {
            WriteMessageToConsole("Info", message);
        }

        public void Error(string message)
        {
            WriteMessageToConsole("Error", message);
        }

        public void Fail(string message)
        {
            WriteMessageToConsole("Fail", message);

            if (FailCallback != null)
            {
                FailCallback();
            }
        }

        public void Warning(string message)
        {
            WriteMessageToConsole("Warning", message);
        }

        public void Break()
        {
            Console.WriteLine();
            Console.WriteLine(DateTime.Now.ToString("F"));
        }

        public void PostError(BaseRequest request, string errorMessage)
        {
            Error(string.Format("{0} failed! {1}", request.Method, errorMessage));
        }
    }
}