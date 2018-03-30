using System;
using Tumba.CanLindaControl.Helpers;
using Tumba.CanLindaControl.Services;

namespace Tumba.CanLindaControl.Model
{
    public class CoinControlStatusReport
    {
        public TimeSpan ExpectedTimeToEarnReward { get; set; }
        public TimeSpan ExpectedTimeToStartStaking
        {
            get
            {
                return UnspentTransactionDateTime.UtcDateTime.AddHours(24) - DateTime.UtcNow;
            }
        }
        public DateTimeOffset OldestRewardDateTime { get; set; }
        public decimal RewardTotal { get; set; }
        public bool Staking { get; set; }
        public CoinControlStatus Status { get; private set; }
        public string StatusMessage { get; private set; }
        public bool CoinControlAddressStaking
        {
            get { return Staking && ExpectedTimeToStartStaking.TotalSeconds <= 0; }
        }
        public DateTimeOffset UnspentTransactionDateTime { get; set; }

        public CoinControlStatusReport()
        {
            ExpectedTimeToEarnReward = TimeSpan.MinValue;
            OldestRewardDateTime = DateTimeOffset.MinValue;
            RewardTotal = 0;
            Staking = false;
            Status = CoinControlStatus.NotReadyWaitingForPaymentToYourself;
            StatusMessage = null;
            UnspentTransactionDateTime = DateTimeOffset.MinValue;
        }

        private static string FormatCoinControlStatusMessage(string statusMessage)
        {
            return string.Format("Coin control status: {0}", statusMessage);
        }

        public void Report(ConsoleMessageHandlingService messageService)
        {
            if (RewardTotal > 0)
            {
                DateTimeOffset nowDate = DateTimeOffset.Now.Date;
                TimeSpan diff = nowDate - OldestRewardDateTime.LocalDateTime.Date;

                messageService.Info(string.Format(
                    "Rewards: {0} LINDA over {1} days = {2} LINDA per day.",
                    Math.Round(RewardTotal, 4),
                    Math.Ceiling(diff.TotalDays),
                    Math.Round(RewardTotal / (decimal)diff.TotalDays, 4)));
            }
            else
            {
                messageService.Info("Rewards: No rewards received.");
            }

            switch (Status)
            {
                case CoinControlStatus.NotReadyOneUnspent:
                {
                    messageService.Info(string.Format(
                        "Staking: {0}.",
                        (CoinControlAddressStaking ? "Yes" : "No")));

                    if (CoinControlAddressStaking)
                    {
                        messageService.Info(string.Format(
                            "Expected time to earn reward: {0} day(s) {1} hour(s).", 
                            ExpectedTimeToEarnReward.Days, 
                            ExpectedTimeToEarnReward.Hours));

                        messageService.Info(string.Format(
                            "Time spent staking: {0} day(s) {1} hour(s) {2} minute(s).",
                            ExpectedTimeToStartStaking.Days * -1,
                            ExpectedTimeToStartStaking.Hours * -1,
                            ExpectedTimeToStartStaking.Minutes * -1));
                    }
                    else
                    {
                        if (ExpectedTimeToStartStaking.TotalSeconds > 0)
                        {
                            messageService.Info(string.Format(
                                "Expected time to start staking: {0} hours {1} minutes.", 
                                Math.Floor(ExpectedTimeToStartStaking.TotalHours), 
                                ExpectedTimeToStartStaking.Minutes));
                        }
                        else
                        {
                            messageService.Warning("You should have already started staking!  Please troubleshoot your wallet.");
                        }
                    }
                    break;
                }
                case CoinControlStatus.NotReadyWaitingForPaymentToYourself:
                case CoinControlStatus.WaitingForStakeToMature:
                case CoinControlStatus.WaitingForUnspentConfirmations:
                case CoinControlStatus.Starting:
                default:
                {
                    break;
                }
            }

            messageService.Info(FormatCoinControlStatusMessage(StatusMessage));
        }

        public void SetStatus(CoinControlStatus status, string statusMessage)
        {
            Status = status;
            StatusMessage = statusMessage;
        }
    }
}