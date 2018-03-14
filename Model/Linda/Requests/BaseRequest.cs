using Newtonsoft.Json;
using System;

namespace Tumba.CanLindaControl.Model.Linda.Requests
{
    public abstract class BaseRequest
    {
        [JsonProperty("jsonrpc")]
        public string JsonRpc { get { return "1.0"; } }

        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("method")]
        public abstract string Method { get; }

        [JsonProperty("params")]
        public abstract object[] MethodParams { get; }
    }
}