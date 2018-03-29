using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
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
        private CoinControlIni m_config;
        private LindaDataConnector m_dataConnector;
        private Timer m_timer;
        private WalletService m_lindaWalletService;
        private object m_processNextLock = new object();
        private string m_walletPassphrase;

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
                m_config.AccountToCoinControl,
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
                    unspent.Account.Equals(m_config.AccountToCoinControl, StringComparison.InvariantCultureIgnoreCase) &&
                    unspent.Confirmations >= m_config.RequiredConfirmations)
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
                m_config.AccountToCoinControl,
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
                if (unspent.Confirmations < m_config.RequiredConfirmations)
                {
                    statusReport.SetStatus(
                        CoinControlStatus.WaitingForUnspentConfirmations,
                        string.Format(
                            "Waiting for more confirmations - {0}/{1} {2} LINDA {3}",
                            unspent.Confirmations,
                            m_config.RequiredConfirmations,
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
                return null;
            }

            if (unspentInNeedOfCoinControl.Count == 1)
            {
                statusReport.SetStatus(
                    CoinControlStatus.NotReadyOneUnspent,
                    "Not ready - Only one unspent transaction");
                
                return statusReport;
            }

            statusReport.SetStatus(
                CoinControlStatus.NotReadyWaitingForPaymentToYourself,
                "Not ready - Waiting for a payment to yourself.");
            
            return statusReport;
        }

        private void DoCoinControl(List<UnspentResponse> unspentInNeedOfCoinControl)
        {
            string toAddress, errorMessage;
            if (!TransactionHelper.TryGetToAddress(unspentInNeedOfCoinControl, out toAddress, out errorMessage))
            {
                MessageService.Fail(errorMessage);
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

            if (!TryUnlockWallet(out errorMessage))
            {
                MessageService.Error(errorMessage);
                return;
            }

            TransactionHelper helper = new TransactionHelper(m_dataConnector);
            string transactionId;
            if (!helper.TrySendFrom(
                m_config.AccountToCoinControl,
                toAddress,
                amountAfterFee,
                out transactionId,
                out errorMessage))
            {
                MessageService.Error(errorMessage);
                return;
            }

            MessageService.Info(string.Format("Coin control transaction sent: {0}.", transactionId));
            MessageService.Info("Coin control complete!");

            if (!TryUnlockWalletForStakingOnly(out errorMessage))
            {
                MessageService.Error(string.Format(
                    "Failed to unlock wallet for staking only!  Wallet may remain entirely unlocked for up to 5 more seconds.  See error: {0}",
                    errorMessage));
            }
        }

        public void Dispose()
        {
            lock(m_processNextLock)
            {
                if (m_timer != null)
                {
                    m_timer.Dispose();
                    m_timer = null;
                }

                if (m_lindaWalletService != null)
                {
                    m_lindaWalletService.Dispose();
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

        private void ProcessNext()
        {
            MessageService.Break();
            MessageService.Info(string.Format("Account: {0}.", m_config.AccountToCoinControl));

            string errorMessage;
            if (!TryUnlockWalletForStakingOnly(out errorMessage))
            {
                MessageService.Error(errorMessage);
                return;
            }

            List<UnspentResponse> unspentInNeedOfCoinControl;
            TransactionHelper helper = new TransactionHelper(m_dataConnector);
            if (!helper.TryGetUnspentInNeedOfCoinControl(
                m_config.AccountToCoinControl,
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

            Console.WriteLine();
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

                m_timer = new Timer();
                m_timer.AutoReset = false;
                m_timer.Interval = m_config.RunFrequencyInMilliSeconds;
                m_timer.Elapsed += TimerElapsed;

                PromptForWalletPassphrase();

                if (m_config.StartLindaWalletExe.Value && !TryStartLindaWallet(out errorMessage))
                {
                    return false;
                }

                if (!TryCheckWalletCompaitibility(out errorMessage))
                {
                    return false;
                }

                if (!TryUnlockWalletForStakingOnly(out errorMessage))
                {
                    return false;
                }

                MessageService.Info(string.Format(
                    "Coin control set to run every {0} milliseconds.", 
                    m_config.RunFrequencyInMilliSeconds));

                ProcessNext();

                m_timer.Start();
                wait.WaitOne();
            }

            return true;
        }

        private void TimerElapsed(object sender, EventArgs args)
        {
            lock(m_processNextLock)
            {
                try
                {
                    ProcessNext();
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
        }

        private bool TryCheckWalletCompaitibility(out string errorMessage)
        {
            WalletHelper helper = new WalletHelper(m_dataConnector);
            return helper.TryCheckWalletCompaitibility(MessageService, out errorMessage);
        }

        private bool TryParseArgs(string[] args, out string errorMessage)
        {
            if (args.Length < 2)
            {
                errorMessage = "Missing required parameters.";
                return false;
            }

            string configPath = args[1].Trim();
            
            CoinControlIni ini;
            if (!IniHelper.TryReadIniFromFile<CoinControlIni>(configPath, out ini, out errorMessage))
            {
                errorMessage = string.Format(
                    "Failed to read coin control config: {0}\r\n{1}",
                    new FileInfo(configPath).FullName,
                    errorMessage);
                
                return false;
            }

            m_config = ini;

            m_dataConnector = new LindaDataConnector(
                m_config.RpcUser,
                m_config.RpcPassword);

            errorMessage = null;
            return true;
        }

        private bool TryStartLindaWallet(out string errorMessage)
        {
            m_lindaWalletService = new WalletService(m_config);

            InfoResponse info;
            InfoRequest requestForInfo = new InfoRequest();
            if (m_dataConnector.TryPost<InfoResponse>(requestForInfo, out info, out errorMessage))
            {
                MessageService.Warning("Looks like your Linda wallet is already running!");
                MessageService.Warning("Stopping your Linda wallet...");

                if (!m_lindaWalletService.TryStopLindaWallet(m_dataConnector, out errorMessage))
                {
                    return false;
                }

                MessageService.Warning("Wallet stopped.");
            }

            MessageService.Info("Starting your Linda wallet...");
            
            if (!m_lindaWalletService.TryStartLindaWallet(m_dataConnector, out errorMessage))
            {
                return false;
            }

            MessageService.Info("Linda wallet startup complete!");

            return true;
        }

        private bool TryUnlockWallet(out string errorMessage)
        {
            WalletHelper helper = new WalletHelper(m_dataConnector);
            return helper.TryUnlockWallet(m_walletPassphrase, 5, false, out errorMessage);
        }

        private bool TryUnlockWalletForStakingOnly(out string errorMessage)
        {
            WalletHelper helper = new WalletHelper(m_dataConnector);

            // Attempt 2 unlocks to work around a wallet bug where the first unlock for staking only seems to unlock the whole wallet.
            if (!helper.TryUnlockWallet(
                m_walletPassphrase,
                m_config.RunFrequencyInMilliSeconds * 3,
                true, 
                out errorMessage))
            {
                return false;
            }

            return helper.TryUnlockWallet(
                m_walletPassphrase,
                m_config.RunFrequencyInMilliSeconds * 3,
                true, 
                out errorMessage);
        }
    }
}