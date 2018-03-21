using System;
using Tumba.CanLindaControl.DataConnectors.Linda;
using Tumba.CanLindaControl.Model.Linda.Requests;
using Tumba.CanLindaControl.Model.Linda.Responses;
using Tumba.CanLindaControl.Services;

namespace Tumba.CanLindaControl.Helpers
{
    public class WalletHelper
    {
        public const string COMPATIBLE_WALLET_VERSIONS = "v1.0.1.3-g";

        private LindaDataConnector m_dataConnector;

        public WalletHelper(LindaDataConnector dataConnector)
        {
            m_dataConnector = dataConnector;
        }

        public bool TryCheckWalletCompaitibility(ConsoleMessageHandlingService messageService, out string errorMessage)
        {
            InfoResponse info;
            InfoRequest requestForInfo = new InfoRequest();

            messageService.Info("Connecting and reading Linda wallet info...");

            if (!m_dataConnector.TryPost<InfoResponse>(requestForInfo, out info, out errorMessage))
            {
                return false;
            }

            messageService.Info("Linda wallet info retrieved!");
            messageService.Info("Checking for wallet compatibility...");
            messageService.Info(string.Format("Compatible versions: {0}", COMPATIBLE_WALLET_VERSIONS));

            if (!COMPATIBLE_WALLET_VERSIONS.Contains(info.Version.ToLower()))
            {
                errorMessage = string.Format(
                    "Linda wallet version: '{0}' is not compatible!",
                    info.Version);
                
                return false;
            }

            messageService.Info(string.Format("Connected wallet version: {0} is compatible!", info.Version));
            return true;
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