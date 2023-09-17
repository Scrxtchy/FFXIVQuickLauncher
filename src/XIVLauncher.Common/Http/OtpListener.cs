using System;
using QRCoder;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;

namespace XIVLauncher.Common.Http
{
    public class OtpListener
    {
        private volatile HttpServer server;

        private const int HTTP_PORT = 4646;

        public event LoginEvent OnOtpReceived;

        public delegate void LoginEvent(string onetimePassword);

        private readonly Thread serverThread;

        public readonly (byte[] qr, string ip)[] qrcodes;

        private QRCodeGenerator qrGenerator = new();

        public OtpListener(string version)
        {
            this.server = new HttpServer(HTTP_PORT, version);
            this.server.GetReceived += this.GetReceived;

            this.serverThread = new Thread(this.server.Start) { Name = "OtpListenerServerThread", IsBackground = true };

            this.qrcodes = PopulateQrList();
        }

        private (byte[] qr, string ip)[] PopulateQrList()
        {
            List<(byte[] qr, string ip)> qrcodes = new();
            qrGenerator.CreateQrCode("test", QRCodeGenerator.ECCLevel.L);
            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface adapter in interfaces)
            {
                if (adapter.OperationalStatus == OperationalStatus.Up)
                {
                    foreach (var ip in adapter.GetIPProperties().UnicastAddresses.Where(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork))
                    {
                        qrcodes.Add((new BitmapByteQRCode(qrGenerator.CreateQrCode($"http://{ip.Address}:{HTTP_PORT}/ffxivlauncher/", QRCodeGenerator.ECCLevel.L)).GetGraphic(1), ip.Address.ToString()));
                    }
                }
            }
            return qrcodes.ToArray();
        }

        private void GetReceived(object sender, HttpServer.HttpServerGetEvent e)
        {
            if (e.Path.StartsWith("/ffxivlauncher/", StringComparison.Ordinal))
            {
                var otp = e.Path.Substring(15);

                OnOtpReceived?.Invoke(otp);
            }
        }

        public void Start()
        {
            this.serverThread.Start();
        }

        public void Stop()
        {
            this.server?.Stop();
        }
    }
}