using System;

namespace Tumba.CanLindaControl.Model
{
    public enum CoinControlStatus
    {
        Starting,
        NotReadyOneUnspent,
        NotReadyWaitingForPaymentToYourself,
        WaitingForUnspentConfirmations,
        WaitingForStakeToMature,
    }
}