using System;
using Tumba.CanLindaControl.DataConnectors.Linda;
using Tumba.CanLindaControl.Model.Linda.Requests;
using Tumba.CanLindaControl.Model.Linda.Responses;

namespace Tumba.CanLindaControl.Services
{
    public class CoinControlService
    {
        public const string COMPATIBLE_WALLET_VERSIONS = "v1.0.1.3-g";
        public const int DEFAULT_FREQUENCY = 60000; // 1 minute

        private LindaDataConnector m_dataConnector;

        public int FrequencyInMilliSeconds { get; private set; }
        public ConsoleMessageHandlingService MessageService { get; private set; }

        public CoinControlService(ConsoleMessageHandlingService messageService)
        {
            MessageService = messageService;
        }

        public void Start()
        {
            InfoResponse info;
            InfoRequest requestForInfo = new InfoRequest();
            string errorMessage;

            MessageService.Info("Connecting and reading Linda wallet info...");

            if (!m_dataConnector.TryPost<InfoResponse>(requestForInfo, out info, out errorMessage))
            {
                MessageService.Fail(errorMessage);
                return;
            }

            MessageService.Info("Linda wallet info retrieved!  Checking for wallet compatibility...");

            if (!COMPATIBLE_WALLET_VERSIONS.Contains(info.Version.ToLower()))
            {
                MessageService.Fail(string.Format(
                    "Linda wallet version: '{0}' is not compatible!  See compatible versions: {1}",
                    info.Version,
                    COMPATIBLE_WALLET_VERSIONS));
                
                return;
            }

            MessageService.Info("Wallet compatibility check complete!");
        }

        public bool TryParseArgs(string[] args, out string errorMessage)
        {
            if (args.Length < 3)
            {
                errorMessage = "Missing required parameters.";
                return false;
            }

            m_dataConnector = new LindaDataConnector(args[1].Trim(), args[2].Trim()); // user, password
            FrequencyInMilliSeconds = DEFAULT_FREQUENCY;

            int tmpFrequency;
            if (args.Length >= 4 && Int32.TryParse(args[3], out tmpFrequency))
            {
                FrequencyInMilliSeconds = tmpFrequency;
            }

            errorMessage = null;
            return true;
        }
    }
}