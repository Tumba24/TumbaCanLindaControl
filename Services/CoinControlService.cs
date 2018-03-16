using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Timers;
using Tumba.CanLindaControl.DataConnectors.Linda;
using Tumba.CanLindaControl.Helpers;
using Tumba.CanLindaControl.Model;
using Tumba.CanLindaControl.Model.Linda.Requests;
using Tumba.CanLindaControl.Model.Linda.Responses;

namespace Tumba.CanLindaControl.Services
{
    public class CoinControlService : IRunnable
    {
        public const string COMPATIBLE_WALLET_VERSIONS = "v1.0.1.3-g";
        public const int DEFAULT_CONFIRMATION_COUNT_REQUIRED_FOR_COIN_CONTROL = 10;
        public const int DEFAULT_FREQUENCY = 60000; // 1 minute

        private LindaDataConnector m_dataConnector;
        private string m_accountToCoinControl;
        private string m_walletPassphrase;
        private Timer m_timer;
        private object m_runProcessLock = new object();

        public int FrequencyInMilliSeconds { get; private set; }
        public ConsoleMessageHandlingService MessageService { get; private set; }

        public CoinControlService(ConsoleMessageHandlingService messageService)
        {
            MessageService = messageService;
        }

        private bool CheckStakingRewards(ref CoinControlStatusReport statusReport)
        {
            string errorMessage;
            List<TransactionResponse> stakingTransactions;
            TransactionHelper helper = new TransactionHelper(m_dataConnector);
            if (!helper.TryGetStakingTransactions(
                m_accountToCoinControl,
                30,
                out stakingTransactions,
                out errorMessage))
            {
                MessageService.Error(errorMessage);
                return false;
            }

            if (stakingTransactions.Count < 1)
            {
                statusReport.RewardTotal = 0;
                return true;
            }

            decimal rewardTotal = 0;
            foreach (TransactionResponse trans in stakingTransactions)
            {
                rewardTotal += trans.Amount;
            }

            statusReport.RewardTotal = rewardTotal;
            statusReport.OldestRewardDateTime = TransactionHelper.GetTransactionTime(
                stakingTransactions[stakingTransactions.Count - 1].Time).LocalDateTime.Date;

            return true;
        }

        private bool CheckStakingInfo(ref CoinControlStatusReport statusReport)
        {
            string errorMessage;

            StakingInfoRequest stakingInfoRequest = new StakingInfoRequest();
            StakingInfoResponse stakingInfoResponse;
            if (!m_dataConnector.TryPost<StakingInfoResponse>(
                stakingInfoRequest,
                out stakingInfoResponse,
                out errorMessage))
            {
                MessageService.Error(errorMessage);
                return false;
            }

            if (!stakingInfoResponse.Enabled)
            {
                MessageService.Warning(string.Format("Staking is disabled!"));
            }

            statusReport.Staking = stakingInfoResponse.Staking;
            

            if (stakingInfoResponse.Staking)
            {
                statusReport.ExpectedTimeToEarnReward = TimeSpan.FromSeconds(stakingInfoResponse.ExpectedTimeInSeconds);
            }
            else
            {
                if (!CheckExpectedTimeToStartStaking(ref statusReport))
                {
                    return false;
                }
            }

            if (!string.IsNullOrEmpty(stakingInfoResponse.Errors))
            {
                MessageService.Error(string.Format("Staking errors found: {0}", stakingInfoResponse.Errors));
            }

            return true;
        }

        private bool CheckExpectedTimeToStartStaking(ref CoinControlStatusReport statusReport)
        {
            string errorMessage;
            ListUnspentRequest unspentRequest = new ListUnspentRequest();
            List<UnspentResponse> unspentResponses;
            if (!m_dataConnector.TryPost<List<UnspentResponse>>(
                unspentRequest, 
                out unspentResponses, 
                out errorMessage))
            {
                MessageService.Error(errorMessage);
                return false;
            }

            long time = 0;
            foreach (UnspentResponse unspent in unspentResponses)
            {
                if (unspent.Account != null && 
                    unspent.Account.Equals(m_accountToCoinControl, StringComparison.InvariantCultureIgnoreCase) &&
                    unspent.Confirmations >= DEFAULT_CONFIRMATION_COUNT_REQUIRED_FOR_COIN_CONTROL)
                {
                    GetTransactionRequest transRequest = new GetTransactionRequest()
                    {
                        TransactionId = unspent.TransactionId
                    };
                    
                    GetTransactionResponse transResponse;
                    if (!m_dataConnector.TryPost(transRequest, out transResponse, out errorMessage))
                    {
                        MessageService.Error(errorMessage);
                        return false;
                    }

                    if (transResponse.Time > time)
                    {
                        time = transResponse.Time;
                    }
                }
            }

            DateTimeOffset transactionTime = TransactionHelper.GetTransactionTime(time);
            statusReport.ExpectedTimeToStartStaking = transactionTime.UtcDateTime.AddHours(24) - DateTime.UtcNow;

            return true;
        }

        private bool CheckWalletCompaitibility()
        {
            InfoResponse info;
            InfoRequest requestForInfo = new InfoRequest();
            string errorMessage;

            MessageService.Info("Connecting and reading Linda wallet info...");

            if (!m_dataConnector.TryPost<InfoResponse>(requestForInfo, out info, out errorMessage))
            {
                MessageService.Fail(errorMessage);
                return false;
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
                
                return false;
            }

            MessageService.Info("Wallet compatibility check complete!");
            return true;
        }

        private CoinControlStatusReport CreateStatusReport(List<UnspentResponse> unspentInNeedOfCoinControl)
        {
            CoinControlStatusReport statusReport = new CoinControlStatusReport();

            if (!CheckStakingRewards(ref statusReport))
            {
                return null;
            }

            string errorMessage;
            List<TransactionResponse> imatureTransactions;
            TransactionHelper helper = new TransactionHelper(m_dataConnector);
            if (!helper.TryGetImatureTransactions(
                m_accountToCoinControl,
                1,
                out imatureTransactions,
                out errorMessage))
            {
                MessageService.Error(errorMessage);
                return null;
            }

            if (imatureTransactions.Count > 0)
            {
                statusReport.SetStatus(
                    CoinControlStatus.WaitingForStakeToMature,
                    string.Format(
                        "Waiting for stake to mature - {0} LINDA {1} confirmations {2}",
                        imatureTransactions[0].Amount,
                        imatureTransactions[0].Confirmations,
                        imatureTransactions[0].TransactionId));

                return statusReport;
            }

            foreach (UnspentResponse unspent in unspentInNeedOfCoinControl)
            {
                if (unspent.Confirmations < DEFAULT_CONFIRMATION_COUNT_REQUIRED_FOR_COIN_CONTROL)
                {
                    statusReport.SetStatus(
                        CoinControlStatus.WaitingForUnspentConfirmations,
                        string.Format(
                            "Waiting for more confirmations - {0}/{1} {2} LINDA {3}",
                            unspent.Confirmations,
                            DEFAULT_CONFIRMATION_COUNT_REQUIRED_FOR_COIN_CONTROL,
                            unspent.Amount,
                            unspent.TransactionId));

                    return statusReport;
                }
            }

            if (unspentInNeedOfCoinControl.Count > 1)
            {
                statusReport.SetStatus(
                    CoinControlStatus.Starting,
                    "Starting...");
                
                return statusReport;
            }

            if (!CheckStakingInfo(ref statusReport))
            {
                return statusReport;
            }

            if (unspentInNeedOfCoinControl.Count == 1)
            {
                statusReport.SetStatus(
                    CoinControlStatus.NotReadyOneUnspent,
                    "Not ready - Only one unspent transaction");
                
                return statusReport;
            }

            statusReport.SetStatus(
                CoinControlStatus.NotReadyNoUnspent,
                "Not ready - No unspent transactions.");
            
            return statusReport;
        }

        private void DoCoinControl(List<UnspentResponse> unspentInNeedOfCoinControl)
        {
            decimal amount = GetAmount(unspentInNeedOfCoinControl);
            decimal fee = GetFee();
            if (fee < 0)
            {
                return;
            }

            decimal amountAfterFee = amount - fee;
            MessageService.Info(string.Format("Amount After Fee: {0} LINDA.", amountAfterFee));

            if (!TryUnlockWallet(5, false))
            {
                return;
            }

            if (!TrySendFrom(
                m_accountToCoinControl,
                unspentInNeedOfCoinControl[0].Address,
                amountAfterFee))
            {
                return;
            }

            TryUnlockWallet(FrequencyInMilliSeconds * 3, true);

            MessageService.Info("Coin control complete!");
        }

        public void Dispose()
        {
            lock(m_runProcessLock)
            {
                if (m_timer != null)
                {
                    m_timer.Dispose();
                    m_timer = null;
                }
            }
        }

        private decimal GetAmount(List<UnspentResponse> unspentInNeedOfCoinControl)
        {
            decimal amount = 0;
            string address = unspentInNeedOfCoinControl[0].Address;
            foreach (UnspentResponse unspent in unspentInNeedOfCoinControl)
            {
                amount += unspent.Amount;
            }

            MessageService.Info(string.Format("Amount: {0} LINDA.", amount));

            return amount;
        }

        private decimal GetFee()
        {
            string errorMessage;
            InfoRequest requestForInfo = new InfoRequest();
            InfoResponse info;
            if (!m_dataConnector.TryPost<InfoResponse>(requestForInfo, out info, out errorMessage))
            {
                MessageService.Error(errorMessage);
                return -1;
            }
            
            MessageService.Info(string.Format("Fee: {0} LINDA.", info.Fee));

            return info.Fee;
        }

        private void PromptForWalletPassphrase()
        {
            Console.WriteLine("Please enter your wallet's passphrase:");

            m_walletPassphrase = string.Empty;

            ConsoleKeyInfo keyInfo;
            while ((keyInfo = Console.ReadKey(true)).Key != ConsoleKey.Enter)
            {
                if (keyInfo.Key == ConsoleKey.Backspace)
                {
                    if (m_walletPassphrase.Length > 0)
                    {
                        m_walletPassphrase = m_walletPassphrase.Remove(m_walletPassphrase.Length -1);
                    }
                }
                else
                {
                    m_walletPassphrase += keyInfo.KeyChar;
                }
            }
        }

        public bool Run(string[] args, out string errorMessage)
        {
            using (System.Threading.ManualResetEvent wait = new System.Threading.ManualResetEvent(false))
            {
                MessageService.FailCallback += (sender, eventArgs) =>
                {
                    wait.Set();
                };

                if (!TryParseArgs(args, out errorMessage))
                {
                    return false;
                }

                Start();

                wait.WaitOne();
            }

            return true;
        }

        private void RunProcess()
        {
            MessageService.Break();
            MessageService.Info(string.Format("Account: {0}.", m_accountToCoinControl));

            if (!TryUnlockWallet(FrequencyInMilliSeconds * 3, true))
            {
                return;
            }

            string errorMessage;
            List<UnspentResponse> unspentInNeedOfCoinControl;
            TransactionHelper helper = new TransactionHelper(m_dataConnector);
            if (!helper.TryGetUnspentInNeedOfCoinControl(
                m_accountToCoinControl,
                out unspentInNeedOfCoinControl,
                out errorMessage))
            {
                MessageService.Error(errorMessage);
                return;
            }

            CoinControlStatusReport statusReport = CreateStatusReport(unspentInNeedOfCoinControl);
            if (statusReport == null)
            {
                return;
            }

            statusReport.Report(MessageService);

            if (statusReport.Status == CoinControlStatus.Starting)
            {
                DoCoinControl(unspentInNeedOfCoinControl);
            }
        }

        private void Start()
        {
            if (!CheckWalletCompaitibility())
            {
                MessageService.Fail();
                return;
            }

            // Attempt 2 unlocks to work around a wallet bug where the first unlock for staking only seems to unlock the whole wallet.
            if (!TryUnlockWallet(FrequencyInMilliSeconds * 3, true || 
                !TryUnlockWallet(FrequencyInMilliSeconds * 3, true)))
            {
                MessageService.Fail();
                return;
            }

            MessageService.Info(string.Format("Coin control set to run every {0} milliseconds.", FrequencyInMilliSeconds));

            RunProcess();
            m_timer.Start();
        }

        private bool TryParseArgs(string[] args, out string errorMessage)
        {
            if (args.Length < 4)
            {
                errorMessage = "Missing required parameters.";
                return false;
            }

            m_dataConnector = new LindaDataConnector(args[1].Trim(), args[2].Trim()); // user, password
            m_accountToCoinControl = args[3].Trim();

            PromptForWalletPassphrase();

            FrequencyInMilliSeconds = DEFAULT_FREQUENCY;

            int tmpFrequency;
            if (args.Length >= 5 && Int32.TryParse(args[4], out tmpFrequency))
            {
                FrequencyInMilliSeconds = tmpFrequency;
            }

            m_timer = new Timer();
            m_timer.AutoReset = false;
            m_timer.Interval = FrequencyInMilliSeconds;
            m_timer.Elapsed += (sender, eventArgs) =>
            {
                lock(m_runProcessLock)
                {
                    try
                    {
                        RunProcess();
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

        private bool TrySendFrom(string fromAccount, string toAddress, decimal amountAfterFee)
        {
            SendFromRequest sendRequest = new SendFromRequest()
            {
                FromAccount = m_accountToCoinControl,
                ToAddress = toAddress,
                AmountAfterFee = amountAfterFee
            };

            string errorMessage;
            string transactionId;
            if (!m_dataConnector.TryPost<string>(sendRequest, out transactionId, out errorMessage))
            {
                MessageService.Error(errorMessage);
                return false;
            }

            MessageService.Info(string.Format("Coin control transaction sent: {0}.", transactionId));
            return true;
        }

        private bool TryUnlockWallet(int timeout, bool forStakingOnly)
        {
            string lockError, errorMessage;

            WalletPassphraseRequest unlockRequest = new WalletPassphraseRequest(m_walletPassphrase);
            unlockRequest.StakingOnly = forStakingOnly;
            unlockRequest.TimeoutInSeconds = timeout;
            if (!m_dataConnector.TryPost<string>(unlockRequest, out lockError, out errorMessage))
            {
                MessageService.Error("Failed to unlock wallet!  Is the passphrase correct?");
                MessageService.Error(errorMessage);
                return false;
            }

            if (!string.IsNullOrEmpty(lockError))
            {
                MessageService.Error(string.Format("Unlock request returned error: {0}", lockError));
                return false;
            }

            return true;
        }
    }
}