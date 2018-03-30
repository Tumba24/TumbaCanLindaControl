using System;
using System.Collections.Generic;
using Tumba.CanLindaControl.DataConnectors.Linda;
using Tumba.CanLindaControl.Model.Linda;
using Tumba.CanLindaControl.Model.Linda.Requests;
using Tumba.CanLindaControl.Model.Linda.Responses;

namespace Tumba.CanLindaControl.Helpers
{
    public class TransactionHelper
    {
        private LindaDataConnector m_dataConnector;

        public TransactionHelper(LindaDataConnector dataConnector)
        {
            m_dataConnector = dataConnector;
        }

        public static DateTimeOffset GetTransactionTime(long time)
        {
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime transactionTime = epoch.AddSeconds(time);

            return new DateTimeOffset(transactionTime);
        }

        public bool TryGetImatureTransactions(
            string address, 
            int numberOfDays, 
            out List<TransactionResponse> transactions, 
            out string errorMessage)
        {
            return TryGetTransactions(
                address,
                "immature", 
                numberOfDays,
                out transactions,
                out errorMessage);
        }

        public bool TryGetStakingTransactions(
            string address, 
            int numberOfDays, 
            out List<TransactionResponse> transactions, 
            out string errorMessage)
        {
            return TryGetTransactions(
                address,
                "generate", 
                numberOfDays,
                out transactions,
                out errorMessage);
        }

        public bool TryGetTransactions(
            string address, 
            string category, 
            int numberOfDays,
            out List<TransactionResponse> transactionsInCategory, 
            out string errorMessage)
        {
            DateTimeOffset localNowDate = DateTimeOffset.Now.Date;
            DateTimeOffset localXDaysAgo = localNowDate.AddDays(numberOfDays * -1);

            transactionsInCategory = new List<TransactionResponse>();

            int count = 10;
            int from = 0;

            DateTimeOffset lastTransactionTime = DateTimeOffset.UtcNow;
            while (lastTransactionTime.LocalDateTime.Date >= localXDaysAgo)
            {
                ListTransactionsRequest listRequest = new ListTransactionsRequest()
                {
                    Account = "*",
                    Count = count,
                    From = from
                };

                List<TransactionResponse> allTransactions;
                if (!m_dataConnector.TryPost<List<TransactionResponse>>(listRequest, out allTransactions, out errorMessage))
                {
                    return false;
                }

                allTransactions.Reverse();

                foreach (TransactionResponse trans in allTransactions)
                {
                    lastTransactionTime = GetTransactionTime(trans.Time);
                    if (trans.Address.Equals(address, StringComparison.CurrentCultureIgnoreCase) &&
                        lastTransactionTime.LocalDateTime.Date >= localXDaysAgo && 
                        trans.Category.Equals(category, StringComparison.InvariantCultureIgnoreCase))
                    {
                        transactionsInCategory.Add(trans);
                    }
                }

                if (allTransactions.Count < count)
                {
                    break;
                }

                from += count;
            }

            errorMessage = null;
            return true;
        }

        public bool TryGetUnspentInNeedOfCoinControl(
            string address,
            out List<UnspentResponse> unspentTransactions,
            out string errorMessage)
        {
            unspentTransactions = new List<UnspentResponse>();

            ListUnspentRequest unspentRequest = new ListUnspentRequest();
            List<UnspentResponse> unspentResponses;
            if (!m_dataConnector.TryPost<List<UnspentResponse>>(
                unspentRequest, 
                out unspentResponses, 
                out errorMessage))
            {
                return false;
            }

            HashSet<string> unspentTransactionIds = new HashSet<string>();
            foreach (UnspentResponse unspent in unspentResponses)
            {
                if (unspent.Address.Equals(address, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (!unspentTransactionIds.Contains(unspent.TransactionId))
                    {
                        unspentTransactionIds.Add(unspent.TransactionId);
                    }

                    unspentTransactions.Add(unspent);
                }
            }

            // Add change transactions.
            foreach (string transactinId in unspentTransactionIds)
            {
                foreach (UnspentResponse change in unspentResponses)
                {
                    if (!change.Address.Equals(address, StringComparison.InvariantCultureIgnoreCase) &&
                        change.TransactionId.Equals(transactinId, StringComparison.InvariantCultureIgnoreCase))
                    {
                        unspentTransactions.Add(change);
                    }
                }
            }

            return true;
        }

        public bool TrySendRawTransaction(
            CreateRawTransactionRequest createRequest,
            out string transactionId,
            out string errorMessage)
        {
            transactionId = null;

            string unsignedHex;
            if (!m_dataConnector.TryPost<string>(createRequest, out unsignedHex, out errorMessage))
            {
                return false;
            }

            SignRawTransactionRequest signRequest = new SignRawTransactionRequest()
            {
                RawHex = unsignedHex
            };
            SignRawTransactionResponse signResponse;
            if (!m_dataConnector.TryPost<SignRawTransactionResponse>(signRequest, out signResponse, out errorMessage))
            {
                return false;
            }

            if (!signResponse.Complete)
            {
                errorMessage = "Sign raw transaction response is incomplete!";
                return false;
            }

            SendRawTransactionRequest sendRequest = new SendRawTransactionRequest()
            {
                RawHex = signResponse.RawHex
            };
            string tmpTransactionId;
            if (!m_dataConnector.TryPost<string>(sendRequest, out tmpTransactionId, out errorMessage))
            {
                return false;
            }

            transactionId = tmpTransactionId;
            return true;
        }
    }
}