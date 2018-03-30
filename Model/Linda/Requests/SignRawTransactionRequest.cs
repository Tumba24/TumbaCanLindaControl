using Newtonsoft.Json;
using System;

namespace Tumba.CanLindaControl.Model.Linda.Requests
{
    public class SignRawTransactionRequest : BaseRequest
    {
        public override string Method { get { return "signrawtransaction"; } }
        public override object[] MethodParams 
        { 
            get 
            { 
                return new object[] { RawHex };
            } 
        }

        [JsonIgnore]
        public string RawHex { get; set; }
    }
}