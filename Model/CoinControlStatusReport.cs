using System;
using Tumba.CanLindaControl.Helpers;
using Tumba.CanLindaControl.Services;

namespace Tumba.CanLindaControl.Model
{
    public class CoinControlStatusReport
    {
        public TimeSpan ExpectedTimeToEarnReward { get; set; }
        public TimeSpan ExpectedTimeToStartStaking { get; set; }
        public DateTimeOffset OldestRewardDateTime { get; set; }
        public decimal RewardTotal { get; set; }
        public bool Staking { get; set; }
        public CoinControlStatus Status { get; private set; }
        public string StatusMessage { get; private set; }

        public CoinControlStatusReport()
        {
            ExpectedTimeToEarnReward = TimeSpan.MinValue;
            ExpectedTimeToStartStaking = TimeSpan.MinValue;
            OldestRewardDateTime = DateTimeOffset.MinValue;
            RewardTotal = 0;
            Staking = false;
            Status = CoinControlStatus.NotReadyNoUnspent;
            StatusMessage = null;
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
                case CoinControlStatus.NotReadyNoUnspent:
                case CoinControlStatus.NotReadyOneUnspent:
                {
                    messageService.Info(string.Format(
                        "Staking: {0}.",
                        (Staking ? "Yes" : "No")));

                    if (Staking)
                    {
                        messageService.Info(string.Format(
                            "Expected time to earn reward: {0} days {1} hours.", 
                            ExpectedTimeToEarnReward.Days, 
                            ExpectedTimeToEarnReward.Hours));
                    }
                    else
                    {
                        if (ExpectedTimeToStartStaking.TotalSeconds > 0)
                        {
                            messageService.Info(string.Format(
                                "Expected time to start staking: {0} hours {1} minutes.", 
                                ExpectedTimeToStartStaking.Hours, 
                                ExpectedTimeToStartStaking.Minutes));
                        }
                        else
                        {
                            messageService.Warning("You should have already started staking!  Please troubleshoot your wallet.");
                        }
                    }
                    break;
                }
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