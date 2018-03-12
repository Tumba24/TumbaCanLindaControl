using System;

namespace Tumba.CanLindaControl.Services
{
    public interface IMessageHandlingService
    {
        void Info(string message);
        void Error(string message);
        void Fail(string message);
        void Warning(string message);
        void Debug(string message);
    }
}