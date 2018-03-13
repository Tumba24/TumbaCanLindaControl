using Newtonsoft.Json;
using System;

namespace Tumba.CanLindaControl.Model.Linda.Responses
{
    public class StakingInfoResponse
    {
        [JsonProperty("enabled")]
        public bool Enabled { get; set; }

        [JsonProperty("staking")]
        public bool Staking { get; set; }

        [JsonProperty("errors")]
        public string Errors { get; set; }

        [JsonProperty("expectedtime")]
        public long ExpectedTimeInSeconds { get; set; }
    }
}