using Newtonsoft.Json;
using System;

namespace Tumba.CanLindaControl.Model.Linda.Responses
{
    public class InfoResponse
    {
        [JsonProperty("version")]
        public string Version { get; set; }
    }
}