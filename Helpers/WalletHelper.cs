using System;
using Tumba.CanLindaControl.DataConnectors.Linda;
using Tumba.CanLindaControl.Model.Linda.Requests;

namespace Tumba.CanLindaControl.Helpers
{
    public class WalletHelper
    {
        private LindaDataConnector m_dataConnector;

        public WalletHelper(LindaDataConnector dataConnector)
        {
            m_dataConnector = dataConnector;
        }

        public bool TryUnlockWallet(string walletPassphrase, int timeout, bool forStakingOnly, out string errorMessage)
        {
            string lockError;

            WalletPassphraseRequest unlockRequest = new WalletPassphraseRequest(walletPassphrase);
            unlockRequest.StakingOnly = forStakingOnly;
            unlockRequest.TimeoutInSeconds = timeout;
            if (!m_dataConnector.TryPost<string>(unlockRequest, out lockError, out errorMessage))
            {
                errorMessage = string.Format(
                    "Failed to unlock wallet!  Is the passphrase correct?  See error: {0}",
                    errorMessage);
                
                return false;
            }

            if (!string.IsNullOrEmpty(lockError))
            {
                errorMessage = string.Format("Unlock request returned error: {0}", lockError);
                return false;
            }

            return true;
        }
    }
}