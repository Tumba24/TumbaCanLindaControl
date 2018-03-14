using Newtonsoft.Json;
using System;

namespace Tumba.CanLindaControl.Model.Linda.Requests
{
    public class WalletPassphraseRequest : BaseRequest
    {
        private string m_passPhrase;

        public WalletPassphraseRequest(string passPhrase)
        {
            m_passPhrase = passPhrase;
        }

        public override string Method { get { return "walletpassphrase"; } }
        public override object[] MethodParams 
        { 
            get 
            { 
                return new object[] {
                    m_passPhrase,
                    TimeoutInSeconds,
                    StakingOnly
                    }; 
            } 
        }

        [JsonIgnore]
        public int TimeoutInSeconds { get; set; }

        [JsonIgnore]
        public bool StakingOnly { get; set; }

    }
}