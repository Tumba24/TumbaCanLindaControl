using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net;
using System.Text;

namespace TumbaCanLindaControl
{
    public class Program
    {
        public const string USAGE = @"Usage:
Before using this utility you must run Linda-qt.exe with the following command:
Linda-qt.exe -server=1 -rpcuser=user -rpcpassword=password -rpcallowip=127.0.0.1 -rpcport=15715

-rpcuser and -rpcpassword should be changed.

Tumba can Linda control command line methods:
coincontrol {rpcuser} {rpcpassword} {frequencyInMilliseconds}";

        public static void Main(string[] args)
        {
            if (args.Length < 1 || 
                args[0].Equals("h", StringComparison.InvariantCultureIgnoreCase) ||
                args[0].Equals("help", StringComparison.InvariantCultureIgnoreCase) ||
                args[0].Equals("?"))
            {
                Console.WriteLine(USAGE);
                return;
            }
        }

        public static void DoTest()
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://localhost:15715");
            request.Credentials = new NetworkCredential("user", "password");
            request.ContentType = "application/json-rpc";
            request.Method = "POST";

            JObject requestObj = new JObject();
            requestObj.Add(new JProperty("jsonrpc", "1.0"));
            requestObj.Add(new JProperty("id", "1"));
            requestObj.Add(new JProperty("method", "getinfo"));

            string requestObjStr = JsonConvert.SerializeObject(requestObj);
            byte[] requestObjData = Encoding.UTF8.GetBytes(requestObjStr);

            request.ContentLength = requestObjData.Length;
            using (Stream requestStream = request.GetRequestStream())
            {
                requestStream.Write(requestObjData, 0, requestObjData.Length);
            }

            using (WebResponse response = request.GetResponse())
            {
                using (Stream responseStream = response.GetResponseStream())
                {
                    byte[] responseObjData = new byte[8000];
                    responseStream.Read(responseObjData, 0, responseObjData.Length);
                    string responseObjStr = Encoding.UTF8.GetString(responseObjData);
                    Console.WriteLine(responseObjStr);
                }
            }
        }
    }
}
