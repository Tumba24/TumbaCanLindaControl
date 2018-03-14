using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
        public const int DEFAULT_CONFIRMATION_COUNT_REQUIRED_FOR_COIN_CONTROL = 10;
        public const int DEFAULT_FREQUENCY = 60000; // 1 minute

        private LindaDataConnector m_dataConnector;
        private string m_accountToCoinControl;
        private string m_walletPassphrase;
        private System.Timers.Timer m_timer;
        private object m_coinControlLock = new object();

        public int FrequencyInMilliSeconds { get; private set; }
        public ConsoleMessageHandlingService MessageService { get; private set; }

        public CoinControlService(ConsoleMessageHandlingService messageService)
        {
            MessageService = messageService;
        }

        private bool CheckIfCoinControlIsNeeded(List<UnspentResponse> unspentInNeedOfCoinControl)
        {
            foreach (UnspentResponse unspent in unspentInNeedOfCoinControl)
            {
                if (unspent.Confirmations < DEFAULT_CONFIRMATION_COUNT_REQUIRED_FOR_COIN_CONTROL)
                {
                    MessageService.Info(string.Format(
                        "Coin control status: Waiting for more confirmations - {0}/{1} {2} LINDA {3}",
                        unspent.Confirmations,
                        DEFAULT_CONFIRMATION_COUNT_REQUIRED_FOR_COIN_CONTROL,
                        unspent.Amount,
                        unspent.TransactionId));

                    return false;
                }
            }

            if (!CheckStakingInfo())
            {
                return false;
            }

            if (unspentInNeedOfCoinControl.Count < 1)
            {
                MessageService.Info("Coin control status: Not ready - No unspent transactions.");
                return false;
            }
            else if (unspentInNeedOfCoinControl.Count == 1)
            {
                MessageService.Info("Coin control status: Not ready - Only one unspent transaction.");
                return false;
            }

            MessageService.Info("Coin control status: starting...");
            return true;
        }

        private bool CheckStakingRewards()
        {
            List<TransactionResponse> stakingTransactions = GetStakingTransactions(30);
            if (stakingTransactions == null)
            {
                return false;
            }

            if (stakingTransactions.Count < 1)
            {
                MessageService.Info("Rewards: No rewards received.");
                return true;
            }

            decimal rewardTotals = 0;
            foreach (TransactionResponse trans in stakingTransactions)
            {
                rewardTotals += trans.Amount;
            }

            DateTimeOffset nowDate = DateTimeOffset.Now.Date;
            DateTimeOffset oldestTransactionDate = GetTransactionTime(stakingTransactions[stakingTransactions.Count - 1].Time).LocalDateTime.Date;

            TimeSpan diff = nowDate - oldestTransactionDate;

            MessageService.Info(string.Format(
                "Rewards: {0} LINDA over {1} days = {2} LINDA per day.",
                Math.Round(rewardTotals, 4),
                Math.Ceiling(diff.TotalDays),
                Math.Round(rewardTotals / (decimal)diff.TotalDays, 4)));

            return true;
        }

        private bool CheckStakingInfo()
        {
            string errorMessage;

            StakingInfoRequest stakingInfoRequest = new StakingInfoRequest();
            StakingInfoResponse stakingInfoResponse;
            if (!m_dataConnector.TryPost<StakingInfoResponse>(
                stakingInfoRequest,
                out stakingInfoResponse,
                out errorMessage))
            {
                MessageService.PostError(stakingInfoRequest, errorMessage);
                return false;
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
            else
            {
                if (!CheckExpectedTimeToStartStaking())
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

        private bool CheckExpectedTimeToStartStaking()
        {
            string errorMessage;
            ListUnspentRequest unspentRequest = new ListUnspentRequest();
            List<UnspentResponse> unspentResponses;
            if (!m_dataConnector.TryPost<List<UnspentResponse>>(
                unspentRequest, 
                out unspentResponses, 
                out errorMessage))
            {
                MessageService.PostError(unspentRequest, errorMessage);
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
                        MessageService.PostError(transRequest, errorMessage);
                        return false;
                    }

                    if (transResponse.Time > time)
                    {
                        time = transResponse.Time;
                    }
                }
            }
            
            DateTimeOffset transactionTime = GetTransactionTime(time);
            TimeSpan stakingDiff = transactionTime.UtcDateTime.AddHours(24) - DateTime.UtcNow;
            if (stakingDiff.TotalSeconds > 0)
            {
                MessageService.Info(string.Format("Expected time to start staking: {0} hours {1} minutes.", stakingDiff.Hours, stakingDiff.Minutes));
            }
            else
            {
                MessageService.Warning("You should have already started staking!  Please troubleshoot your wallet.");
            }

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

        private void DoCoinControl()
        {
            MessageService.Break();
            MessageService.Info(string.Format("Account: {0}.", m_accountToCoinControl));

            if (!TryUnlockWallet(FrequencyInMilliSeconds * 3, true))
            {
                return;
            }

            if (!CheckStakingRewards())
            {
                return;
            }

            List<UnspentResponse> unspentInNeedOfCoinControl = GetUnSpentInNeedOfCoinControl();
            if (unspentInNeedOfCoinControl == null)
            {
                return;
            }

            if (!CheckIfCoinControlIsNeeded(unspentInNeedOfCoinControl))
            {
                return;
            }

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

            MessageService.Info("Coin control status: complete!");
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
                MessageService.PostError(requestForInfo, errorMessage);
                return -1;
            }
            
            MessageService.Info(string.Format("Fee: {0} LINDA.", info.Fee));

            return info.Fee;
        }

        private List<TransactionResponse> GetStakingTransactions(int numberOfDays)
        {
            DateTimeOffset localNowDate = DateTimeOffset.Now.Date;
            DateTimeOffset localXDaysAgo = localNowDate.AddDays(numberOfDays * -1);

            List<TransactionResponse> stakingTransactions = new List<TransactionResponse>();

            int count = 10;
            int from = 0;

            DateTimeOffset lastTransactionTime = DateTimeOffset.UtcNow;
            while (lastTransactionTime.LocalDateTime.Date >= localXDaysAgo)
            {
                string errorMessage;
                ListTransactionsRequest listRequest = new ListTransactionsRequest()
                {
                    Account = m_accountToCoinControl,
                    Count = count,
                    From = from
                };

                List<TransactionResponse> transactions;
                if (!m_dataConnector.TryPost<List<TransactionResponse>>(listRequest, out transactions, out errorMessage))
                {
                    MessageService.PostError(listRequest, errorMessage);
                    return null;
                }

                transactions.Reverse();

                foreach (TransactionResponse trans in transactions)
                {
                    lastTransactionTime = GetTransactionTime(trans.Time);
                    if (lastTransactionTime.LocalDateTime.Date >= localXDaysAgo && 
                        trans.Category.Equals("generate", StringComparison.InvariantCultureIgnoreCase))
                    {
                        stakingTransactions.Add(trans);
                    }
                }

                if (transactions.Count < count)
                {
                    break;
                }

                from += count;
            }

            return stakingTransactions;
        }

        public DateTimeOffset GetTransactionTime(long time)
        {
            DateTime epoch = new DateTime(1970, 1, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime transactionTime = epoch.AddSeconds(time);

            return new DateTimeOffset(transactionTime);
        }

        private List<UnspentResponse> GetUnSpentInNeedOfCoinControl()
        {
            string errorMessage;
            ListUnspentRequest unspentRequest = new ListUnspentRequest();
            List<UnspentResponse> unspentResponses;
            if (!m_dataConnector.TryPost<List<UnspentResponse>>(
                unspentRequest, 
                out unspentResponses, 
                out errorMessage))
            {
                MessageService.PostError(unspentRequest, errorMessage);
                return null;
            }

            List<UnspentResponse> unspentForAccount = new List<UnspentResponse>();
            foreach (UnspentResponse unspent in unspentResponses)
            {
                if (unspent.Account != null && 
                    unspent.Account.Equals(m_accountToCoinControl, StringComparison.InvariantCultureIgnoreCase))
                {
                    unspentForAccount.Add(unspent);
                }
            }

            return unspentForAccount;
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

        private void Start()
        {
            if (!CheckWalletCompaitibility())
            {
                return;
            }

            // Attempt 2 unlocks to work around a wallet bug where the first unlock for staking only seems to unlock the whole wallet.
            if (!TryUnlockWallet(FrequencyInMilliSeconds * 3, true || 
                !TryUnlockWallet(FrequencyInMilliSeconds * 3, true)))
            {
                return;
            }

            MessageService.Info(string.Format("Coin control set to run every {0} milliseconds.", FrequencyInMilliSeconds));

            DoCoinControl();
            m_timer.Start();
        }

        private bool TryParseArgs(string[] args, out string errorMessage)
        {
            if (args.Length < 5)
            {
                errorMessage = "Missing required parameters.";
                return false;
            }

            m_dataConnector = new LindaDataConnector(args[1].Trim(), args[2].Trim()); // user, password
            m_accountToCoinControl = args[3].Trim();
            m_walletPassphrase = args[4].Trim();
            FrequencyInMilliSeconds = DEFAULT_FREQUENCY;

            int tmpFrequency;
            if (args.Length >= 6 && Int32.TryParse(args[5], out tmpFrequency))
            {
                FrequencyInMilliSeconds = tmpFrequency;
            }

            m_timer = new System.Timers.Timer();
            m_timer.AutoReset = false;
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
                MessageService.PostError(sendRequest, errorMessage);
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
                MessageService.PostError(unlockRequest, errorMessage);
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