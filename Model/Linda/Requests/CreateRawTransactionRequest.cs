using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using Tumba.CanLindaControl.Model.Linda;

namespace Tumba.CanLindaControl.Model.Linda.Requests
{
    public class CreateRawTransactionRequest : BaseRequest
    {
        public override string Method { get { return "createrawtransaction"; } }
        public override object[] MethodParams 
        { 
            get 
            {
                JObject output = new JObject();
                output.Add(ToAddress, AmountAfterFee);

                return new object[] {
                    Inputs,
                    output,
                    }; 
            } 
        }

        [JsonIgnore]
        public decimal AmountAfterFee { get; private set; }

        [JsonIgnore]
        public List<TransactionInput> Inputs { get; set; }

        [JsonIgnore]
        public string ToAddress { get; private set; }

        public void SendFullAmountTo(string address, decimal fee)
        {
            decimal fullAmount = 0;
            foreach (TransactionInput input in Inputs)
            {
                fullAmount += input.AmountAvailable;
            }

            ToAddress = address;
            AmountAfterFee = fullAmount - fee;
        }
    }
}