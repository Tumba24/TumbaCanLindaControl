using System;
using System.Collections.Generic;
using Tumba.CanLindaControl.DataConnectors.Linda;
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
            string account, 
            int numberOfDays, 
            out List<TransactionResponse> transactions, 
            out string errorMessage)
        {
            return TryGetTransactions(
                account,
                "immature", 
                numberOfDays,
                out transactions,
                out errorMessage);
        }

        public bool TryGetStakingTransactions(
            string account, 
            int numberOfDays, 
            out List<TransactionResponse> transactions, 
            out string errorMessage)
        {
            return TryGetTransactions(
                account,
                "generate", 
                numberOfDays,
                out transactions,
                out errorMessage);
        }

        public bool TryGetTransactions(
            string account, 
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
                    Account = account,
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
                    if (lastTransactionTime.LocalDateTime.Date >= localXDaysAgo && 
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
            string account,
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

            foreach (UnspentResponse unspent in unspentResponses)
            {
                if (unspent.Account != null && 
                    unspent.Account.Equals(account, StringComparison.InvariantCultureIgnoreCase))
                {
                    unspentTransactions.Add(unspent);
                }
            }

            return true;
        }
    }
}