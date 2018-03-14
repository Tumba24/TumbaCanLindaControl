using Newtonsoft.Json;
using System;

namespace Tumba.CanLindaControl.Model.Linda.Requests
{
    public class ListTransactionsRequest : BaseRequest
    {
        public override string Method { get { return "listtransactions"; } }
        public override object[] MethodParams 
        { 
            get 
            { 
                return new object[] {
                    Account,
                    Count,
                    From
                    }; 
            } 
        }

        [JsonIgnore]
        public string Account { get; set; }

        [JsonIgnore]
        public int Count { get; set; }

        [JsonIgnore]
        public int From { get; set; }
    }
}