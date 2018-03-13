using System;
using System.Collections.Generic;
using System.Threading;
using System.Timers;
using Tumba.CanLindaControl.DataConnectors.Linda;
using Tumba.CanLindaControl.Model.Linda.Requests;
using Tumba.CanLindaControl.Model.Linda.Responses;

namespace Tumba.CanLindaControl.Services
{
    public class CoinControlService : IDisposable
    {
        public const string COMPATIBLE_WALLET_VERSIONS = "v1.0.1.3-g";
        public const int DEFAULT_FREQUENCY = 60000; // 1 minute

        private LindaDataConnector m_dataConnector;
        private string m_accountToCoinControl;
        private System.Timers.Timer m_timer;
        private object m_coinControlLock = new object();

        public int FrequencyInMilliSeconds { get; private set; }
        public ConsoleMessageHandlingService MessageService { get; private set; }

        public CoinControlService(ConsoleMessageHandlingService messageService)
        {
            MessageService = messageService;
        }

        public void DoCoinControl()
        {
            MessageService.Break();

            // TODO make sure wallet is unlocked for staking.
            MessageService.Info(string.Format("Account: {0}.", m_accountToCoinControl));

            string errorMessage;
            Tumba.CanLindaControl.Model.Linda.Requests.
            StakingInfoRequest stakingInfoRequest = new StakingInfoRequest();
            StakingInfoResponse stakingInfoResponse;
            if (!m_dataConnector.TryPost<StakingInfoResponse>(
                stakingInfoRequest,
                out stakingInfoResponse,
                out errorMessage))
            {
                MessageService.Error(string.Format("{0} failed!", stakingInfoRequest.Method));
                return;
            }

            if (!stakingInfoResponse.Enabled)
            {
                MessageService.Warning(string.Format("Staking is disabled!"));
            }

            MessageService.Info(string.Format("Staking: {0}.",
                (stakingInfoResponse.Staking ? "Yes" : "No")));

            if (stakingInfoResponse.Staking)
            {
                TimeSpan timeSpan = TimeSpan.FromSeconds(stakingInfoResponse.ExpectedTimeInSeconds);
                MessageService.Info(string.Format("Expected time to earn reward: {0} days {1} hours.", timeSpan.Days, timeSpan.Hours));
            }

            if (!string.IsNullOrEmpty(stakingInfoResponse.Errors))
            {
                MessageService.Error(string.Format("Staking errors found: {0}", stakingInfoResponse.Errors));
            }

            ListUnspentRequest unspentRequest = new ListUnspentRequest();
            List<UnspentResponse> unspentResponses;
            if (!m_dataConnector.TryPost<List<UnspentResponse>>(
                unspentRequest, 
                out unspentResponses, 
                out errorMessage))
            {
                MessageService.Error(string.Format("{0} failed!", unspentRequest.Method));
                return;
            }

            decimal amount = 0;
            string address = null;
            List<UnspentResponse> unspentForAccount = new List<UnspentResponse>();
            foreach (UnspentResponse unspent in unspentResponses)
            {
                if (unspent.Account != null && 
                    unspent.Account.Equals(m_accountToCoinControl, StringComparison.InvariantCultureIgnoreCase))
                {
                    amount += unspent.Amount;
                    address = unspent.Address;
                    unspentForAccount.Add(unspent);
                }
            }

            if (unspentForAccount.Count < 1)
            {
                MessageService.Error("No unspent transactions!");
                return;
            }

            if (unspentForAccount.Count == 1)
            {
                MessageService.Info("Only one unspent transaction.");
                MessageService.Info("No need for coin control.");
                return;
            }

            MessageService.Info("Coin control needed.  Starting...");
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

            MessageService.Info("Linda wallet info retrieved!");
            MessageService.Info("Checking for wallet compatibility...");

            if (!COMPATIBLE_WALLET_VERSIONS.Contains(info.Version.ToLower()))
            {
                MessageService.Fail(string.Format(
                    "Linda wallet version: '{0}' is not compatible!",
                    info.Version));

                MessageService.Fail(string.Format(
                    "See compatible versions: {0}",
                    COMPATIBLE_WALLET_VERSIONS));
                
                return;
            }

            MessageService.Info("Wallet compatibility check complete!");

            DoCoinControl();

            MessageService.Info(string.Format("Coin control set to run every {0} milliseconds.", FrequencyInMilliSeconds));
            m_timer.Start();
        }

        public bool TryParseArgs(string[] args, out string errorMessage)
        {
            if (args.Length < 4)
            {
                errorMessage = "Missing required parameters.";
                return false;
            }

            m_dataConnector = new LindaDataConnector(args[1].Trim(), args[2].Trim()); // user, password
            m_accountToCoinControl = args[3].Trim();
            FrequencyInMilliSeconds = DEFAULT_FREQUENCY;

            int tmpFrequency;
            if (args.Length >= 5 && Int32.TryParse(args[4], out tmpFrequency))
            {
                FrequencyInMilliSeconds = tmpFrequency;
            }

            m_timer = new System.Timers.Timer();
            m_timer.AutoReset = false;
            m_timer.Enabled = true;
            m_timer.Interval = FrequencyInMilliSeconds;
            m_timer.Elapsed += (sender, eventArgs) =>
            {
                lock(m_coinControlLock)
                {
                    try
                    {
                        DoCoinControl();
                        if (m_timer != null)
                        {
                            m_timer.Start();
                        }
                    }
                    catch (Exception exception)
                    {
                        MessageService.Fail(string.Format("Coin control failed!  See exception: {0}", exception));
                    }
                }
            };

            errorMessage = null;
            return true;
        }

        public void Dispose()
        {
            lock(m_coinControlLock)
            {
                if (m_timer != null)
                {
                    m_timer.Dispose();
                    m_timer = null;
                }
            }
        }

        public static void Run(string[] args)
        {
            using (ManualResetEvent wait = new ManualResetEvent(false))
            {
                ConsoleMessageHandlingService messageHandler = new ConsoleMessageHandlingService(() =>
                {
                    wait.Set();
                });

                using (CoinControlService service = new CoinControlService(messageHandler))
                {
                    string errorMessage;
                    if (!service.TryParseArgs(args, out errorMessage))
                    {
                        Console.WriteLine(errorMessage);
                        Environment.Exit(-2);
                    }
                    service.Start();

                    wait.WaitOne();
                }
            }
        }
    }
}