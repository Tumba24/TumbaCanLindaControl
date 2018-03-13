using Newtonsoft.Json;
using System;

namespace Tumba.CanLindaControl.Model.Linda.Responses
{
    public class TransactionResponse
    {
        [JsonProperty("account")]
        public string Account { get; set; }  

        [JsonProperty("address")]
        public string Address { get; set; }  

        [JsonProperty("category")]
        public string Category { get; set; }  

        [JsonProperty("amount")]
        public decimal Amount { get; set; }  

        [JsonProperty("confirmations")]
        public long Confirmations { get; set; }  

    }
}