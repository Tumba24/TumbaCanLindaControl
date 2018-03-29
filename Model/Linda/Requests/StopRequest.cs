using System;

namespace Tumba.CanLindaControl.Model.Linda.Requests
{
    public class StopRequest : BaseRequest
    {
        public override string Method { get { return "stop"; } }
        public override object[] MethodParams { get { return new string[0]; } }
    }
}