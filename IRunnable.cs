using System;

namespace Tumba.CanLindaControl
{
    public interface IRunnable : IDisposable
    {
        bool Run(string[] args, out string errorMessage);
    }
}