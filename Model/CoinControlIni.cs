using System;
using System.Collections.Generic;

namespace Tumba.CanLindaControl.Model
{
    public class CoinControlIniKeys
    {
        public const string AccountToCoinControl = "AccountToCoinControl";
        public const string LindaWalletAdditionalArgs = "LindaWalletAdditionalArgs";
        public const string LindaWalletExeFilePath = "LindaWalletExeFilePath";
        public const string RequiredConfirmations = "RequiredConfirmations";
        public const string RpcPassword = "RpcPassword";
        public const string RpcUser = "RpcUser";
        public const string RunFrequencyInMilliSeconds = "RunFrequencyInMilliSeconds";
        public const string StartLindaWalletExe = "StartLindaWalletExe";
    }
    public class CoinControlIni : BaseIni
    {
        public string AccountToCoinControl
        {
            get
            {
                return ParseStringValue(CoinControlIniKeys.AccountToCoinControl);
            }
        }

        public string LindaWalletAdditionalArgs
        {
            get
            {
                return ParseStringValue(CoinControlIniKeys.LindaWalletAdditionalArgs);
            }
        }

        public string LindaWalletExeFilePath
        {
            get
            {
                return ParseStringValue(CoinControlIniKeys.LindaWalletExeFilePath);
            }
        }

        public int RequiredConfirmations
        {
            get
            {
                return ParseInt32Value(CoinControlIniKeys.RequiredConfirmations);
            }
        }

        public string RpcPassword
        {
            get
            {
                return ParseStringValue(CoinControlIniKeys.RpcPassword);
            }
        }

        public string RpcUser
        {
            get
            {
                return ParseStringValue(CoinControlIniKeys.RpcUser);
            }
        }

        public int RunFrequencyInMilliSeconds
        {
            get
            {
                return ParseInt32Value(CoinControlIniKeys.RunFrequencyInMilliSeconds);
            }
        }

        public bool? StartLindaWalletExe
        {
            get
            {
                return ParseBoolValue(CoinControlIniKeys.StartLindaWalletExe);
            }
        }

        public override bool ValidateIni(out List<string> errors)
        {
            errors = new List<string>();

            if (!ValidateStrValue(AccountToCoinControl))
            {
                AddValueError(errors, CoinControlIniKeys.AccountToCoinControl, typeof(string));
            }

            if (!ValidateStrValue(LindaWalletAdditionalArgs))
            {
                AddValueError(errors, CoinControlIniKeys.LindaWalletAdditionalArgs, typeof(string));
            }

            if (!ValidateStrValue(LindaWalletExeFilePath))
            {
                AddValueError(errors, CoinControlIniKeys.LindaWalletExeFilePath, typeof(string));
            }

            if (!ValidateInt32Value(RequiredConfirmations))
            {
                AddValueError(errors, CoinControlIniKeys.RequiredConfirmations, typeof(int));
            }

            if (!ValidateStrValue(RpcPassword))
            {
                AddValueError(errors, CoinControlIniKeys.RpcPassword, typeof(string));
            }

            if (!ValidateStrValue(RpcUser))
            {
                AddValueError(errors, CoinControlIniKeys.RpcUser, typeof(string));
            }

            if (!ValidateInt32Value(RunFrequencyInMilliSeconds))
            {
                AddValueError(errors, CoinControlIniKeys.RunFrequencyInMilliSeconds, typeof(int));
            }

            if (!ValidateBoolValue(StartLindaWalletExe))
            {
                AddValueError(errors, CoinControlIniKeys.StartLindaWalletExe, typeof(bool));
            }

            return errors.Count < 1;
        }
    }
}