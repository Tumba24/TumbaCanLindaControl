using Newtonsoft.Json;
using System;

namespace Tumba.CanLindaControl.Model.Linda.Responses
{
    public class GetTransactionResponse
    {
        [JsonProperty("time")]
        public long Time { get; set; }
    }
}