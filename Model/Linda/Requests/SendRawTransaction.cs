using Newtonsoft.Json;
using System;

namespace Tumba.CanLindaControl.Model.Linda.Requests
{
    public class SendRawTransactionRequest : BaseRequest
    {
        public override string Method { get { return "sendrawtransaction"; } }
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