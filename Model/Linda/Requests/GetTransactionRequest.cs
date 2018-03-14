using Newtonsoft.Json;
using System;

namespace Tumba.CanLindaControl.Model.Linda.Requests
{
    public class GetTransactionRequest : BaseRequest
    {
        public override string Method { get { return "gettransaction"; } }
        public override object[] MethodParams 
        { 
            get 
            { 
                return new object[] { TransactionId };
            } 
        }

        [JsonIgnore]
        public string TransactionId { get; set; }
    }
}