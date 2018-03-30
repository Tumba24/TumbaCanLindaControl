using Newtonsoft.Json;
using System;

namespace Tumba.CanLindaControl.Model.Linda.Responses
{
    public class SignRawTransactionResponse
    {
        [JsonProperty("complete")]
        public bool Complete { get; set; }
        
        [JsonProperty("hex")]
        public string RawHex { get; set; }
    }
}