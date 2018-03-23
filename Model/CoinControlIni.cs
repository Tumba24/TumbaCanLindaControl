using System;
using System.Collections.Generic;

namespace Tumba.CanLindaControl.Model
{
    public class CoinControlIniKeys
    {
        public const string AccountToCoinControl = "AccountToCoinControl";
        public const string RequiredConfirmations = "RequiredConfirmations";
        public const string RpcPassword = "RpcPassword";
        public const string RpcUser = "RpcUser";
        public const string RunFrequencyInMilliSeconds = "RunFrequencyInMilliSeconds";
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

        public override bool ValidateIni(out List<string> errors)
        {
            errors = new List<string>();

            if (!ValidateStrValue(AccountToCoinControl))
            {
                AddValueError(errors, CoinControlIniKeys.AccountToCoinControl, typeof(string));
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

            return errors.Count > 0;
        }
    }
}