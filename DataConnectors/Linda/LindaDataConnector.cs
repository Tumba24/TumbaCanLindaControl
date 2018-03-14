using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net;
using System.Text;
using Tumba.CanLindaControl.Model.Linda.Requests;

namespace Tumba.CanLindaControl.DataConnectors.Linda
{
    public class LindaDataConnector
    {
        private string m_rpcUser;
        private string m_rpcPassword;

        public int NextRequestId { get; private set; }

        public LindaDataConnector(string rpcUser, string rpcPassword)
        {
            m_rpcUser = rpcUser;
            m_rpcPassword = rpcPassword;
            NextRequestId = 1;
        }

        public bool TryPost<T>(BaseRequest requestObj, out T responseObj, out string errorMessage)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://localhost:33821");
            request.Credentials = new NetworkCredential(m_rpcUser, m_rpcPassword);
            request.ContentType = "application/json-rpc";
            request.Method = "POST";

            requestObj.Id = NextRequestId++;

            string requestObjStr = JsonConvert.SerializeObject(requestObj);
            byte[] requestObjData = Encoding.UTF8.GetBytes(requestObjStr);

            request.ContentLength = requestObjData.Length;

            string responseObjStr = null;
            try
            {
                using (Stream requestStream = request.GetRequestStream())
                {
                    requestStream.Write(requestObjData, 0, requestObjData.Length);
                }

                using (WebResponse response = request.GetResponse())
                {
                    using (Stream responseStream = response.GetResponseStream())
                    {
                        byte[] responseObjData = new byte[response.ContentLength];
                        responseStream.Read(responseObjData, 0, responseObjData.Length);
                        responseObjStr = Encoding.UTF8.GetString(responseObjData);
                    }
                }
            }
            catch (Exception exception)
            {
                responseObj = default(T);
                errorMessage = string.Format("JSON RPC call failed!  See exception: {0}", exception);
                return false;
            }

            JObject rawResponse = JObject.Parse(responseObjStr);
            JToken errorToken = rawResponse.GetValue("error");
            if (errorToken != null)
            {
                errorMessage = errorToken.ToObject<string>();
                if (!string.IsNullOrEmpty(errorMessage))
                {
                    responseObj = default(T);
                    return false;
                }
            }

            try
            {
                JToken resultToken = rawResponse.GetValue("result");
                responseObj = resultToken.ToObject<T>();
            }
            catch (Exception exception)
            {
                responseObj = default(T);
                errorMessage = string.Format("Response deserialization failed!  See exception: {0}", exception);
                return false;
            }

            errorMessage = null;
            return true;
        }
    }
}