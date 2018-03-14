using System;

namespace Tumba.CanLindaControl.Model.Linda.Requests
{
    public class StakingInfoRequest : BaseRequest
    {
        public override string Method { get { return "getstakinginfo"; } }
        public override object[] MethodParams { get { return new string[0]; } }
    }
}