using System;

namespace Tumba.CanLindaControl
{
    public interface IRunnable
    {
        bool Run(string[] args, out string errorMessage);
    }
}