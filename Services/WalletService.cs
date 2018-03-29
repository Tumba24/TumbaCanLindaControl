using System;
using System.Diagnostics;
using System.IO;
using Tumba.CanLindaControl.DataConnectors.Linda;
using Tumba.CanLindaControl.Model;
using Tumba.CanLindaControl.Model.Linda.Requests;
using Tumba.CanLindaControl.Model.Linda.Responses;

namespace Tumba.CanLindaControl.Services
{
    public class WalletService : IDisposable
    {
        public const int WALLET_WAIT_ATTEMPTS = 240;
        public const int WALLET_WAIT_MS = 250;
        private Process m_lindaWalletProcess;

        public bool ExitProcessed { get; private set; }
        public FileInfo LindaWalletExeFileInfo { get; private set; }
        public ProcessStartInfo LindaWalletStartInfo { get; private set; }
        public event EventHandler ProcessExitedCallback;

        public WalletService(CoinControlIni coinControlIni)
        {
            ExitProcessed = false;
            LindaWalletExeFileInfo = new FileInfo(coinControlIni.LindaWalletExeFilePath);

            LindaWalletStartInfo = new ProcessStartInfo()
            {
                FileName = LindaWalletExeFileInfo.FullName,
                Arguments = string.Format(
                    "-rpcuser=\"{0}\" -rpcpassword=\"{1}\" {2}",
                    coinControlIni.RpcUser,
                    coinControlIni.RpcPassword,
                    coinControlIni.LindaWalletAdditionalArgs)
            };
        }

        public void Dispose()
        {
            if (m_lindaWalletProcess != null)
            {
                if (!m_lindaWalletProcess.HasExited)
                {
                    m_lindaWalletProcess.Kill();
                }
                m_lindaWalletProcess.Dispose();
            }
        }

        public bool IsUp(LindaDataConnector dataConnector)
        {
            string errorMessage;
            InfoRequest request = new InfoRequest();
            InfoResponse response;

            return dataConnector.TryPost<InfoResponse>(request, out response, out errorMessage);
        }

        private void ProcessExited(object sender, EventArgs args)
        {
            EventHandler handler = ProcessExitedCallback;
            if (!ExitProcessed && handler != null)
            {
                handler.Invoke(sender, args);
            }

            ExitProcessed = true;
        }

        public bool TryStartLindaWallet(LindaDataConnector dataConnector, out string errorMessage)
        {
            if (!LindaWalletExeFileInfo.Exists)
            {
                errorMessage = string.Format("Linda wallet exe file not found at: {0}", LindaWalletExeFileInfo.FullName);
                return false;
            }

            try
            {
                m_lindaWalletProcess = new Process();
                m_lindaWalletProcess.StartInfo = LindaWalletStartInfo;
                m_lindaWalletProcess.EnableRaisingEvents = true;
                m_lindaWalletProcess.Exited += ProcessExited;
                m_lindaWalletProcess.Disposed += ProcessExited;

                m_lindaWalletProcess.Start();
            }
            catch (Exception exception)
            {
                errorMessage = string.Format("Failed to start linda wallet!  See exception: {0}", exception);
                return false;
            }

            if (!WaitForWalletToComeUp(dataConnector))
            {
                errorMessage = "Wallet won't start!";
                return false;
            }

            errorMessage = null;
            return true;
        }

        public bool TryStopLindaWallet(LindaDataConnector dataConnector, out string errorMessage)
        {
            StopRequest request = new StopRequest();
            string responseStr;
            if (!dataConnector.TryPost<string>(request, out responseStr, out errorMessage))
            {
                return false;
            }

            if (!WaitForWalletToGoDown(dataConnector))
            {
                errorMessage = "Wallet won't stop!";
                return false;
            }

            return true;
        }

        public bool WaitForWalletToComeUp(LindaDataConnector dataConnector)
        {
            int attempt = 0;

            while (attempt < WALLET_WAIT_ATTEMPTS)
            {
                attempt++;
                if (IsUp(dataConnector))
                {
                    return true;
                }

                System.Threading.Thread.Sleep(WALLET_WAIT_MS);
            }

            return false;
        }

        public bool WaitForWalletToGoDown(LindaDataConnector dataConnector)
        {
            int attempt = 0;

            while (attempt < WALLET_WAIT_ATTEMPTS)
            {
                attempt++;
                if (!IsUp(dataConnector))
                {
                    return true;
                }

                System.Threading.Thread.Sleep(WALLET_WAIT_MS);
            }

            return false;
        }
    }
}