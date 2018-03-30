using Newtonsoft.Json;
using System;
using Tumba.CanLindaControl.Model.Linda.Responses;

namespace Tumba.CanLindaControl.Model.Linda
{
    public class TransactionInput
    {
        [JsonIgnore()]
        public decimal AmountAvailable { get; set; }

        [JsonProperty("vout")]
        public int OutputIndex { get; set; }

        [JsonProperty("txid")]
        public string TransactionId { get; set; }

        public static TransactionInput CreateFromUnspent(UnspentResponse response)
        {
            return new TransactionInput()
            {
                AmountAvailable = response.Amount,
                OutputIndex = response.OutputIndex,
                TransactionId = response.TransactionId
            };
        }
    }
}