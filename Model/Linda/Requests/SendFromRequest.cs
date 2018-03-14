using Newtonsoft.Json;
using System;

namespace Tumba.CanLindaControl.Model.Linda.Requests
{
    public class SendFromRequest : BaseRequest
    {
        public override string Method { get { return "sendfrom"; } }
        public override object[] MethodParams 
        { 
            get 
            { 
                return new object[] {
                    FromAccount,
                    ToAddress,
                    AmountAfterFee
                    }; 
            } 
        }

        [JsonIgnore]
        public string FromAccount { get; set; }

        [JsonIgnore]
        public string ToAddress { get; set; }

        [JsonIgnore]
        public decimal AmountAfterFee { get; set; }
    }
}