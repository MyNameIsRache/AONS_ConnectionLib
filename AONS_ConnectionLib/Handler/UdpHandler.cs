using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using AONS_ConnectionLib.Structures;
using AONS_ConnectionLib.Structures.Exceptions;

namespace AONS_ConnectionLib
{
    public class UdpHandler : BaseHandler, IDisposable
    {
        UdpClient udpClient = null!;

        protected override object GetListener => udpClient;

        public void Dispose()
        {
            udpClient?.Dispose();
        }

        public override void StopListener()
        {
            udpClient?.Close();
        }

        protected override void CreateListener()
        {
            udpClient = new UdpClient(Port);

            if (!IsListenerStopped)
                StopListener();
        }

        protected override void DoListenerLoop()
        {

            while (_KeepListenerRunning)
            {
                IPEndPoint ipEnd = new IPEndPoint(IPAddress.Any, Port);

                try
                {
                    var msg = udpClient.Receive(ref ipEnd);
                    ReadConnection(DataPacket.CreateDataPacket(msg));
                }
                catch (Exception) { }
            }
        }

        public void SendMessagee(string pIPAddress, int pPort, string pMessage)
        {
            SendMessage(pIPAddress, pPort, pMessage);
        }

        public static void SendMessage(string pIPAddress, int pPort, string pMessage)
        {
            if (IPAddress.TryParse(pIPAddress, out IPAddress? pIP))
                SendMessage(new IPEndPoint(pIP, pPort), new DataPacket(pMessage));
            else
                throw new IPInvalidException($"Was not able to parse {pIPAddress}");
        }

        public static void SendMessage(IPEndPoint pIPAddress, string pMessage)
        {
            SendMessage(pIPAddress, new DataPacket(pMessage));
        }

        private static void SendMessage(IPEndPoint IPEnd, DataPacket pDP)
        {
            using (UdpClient udpClient = new UdpClient())
            {
                udpClient.Connect(IPEnd);

                SendMessage(udpClient, pDP);
            }
        }

        public static void SendMessage(UdpClient pClient, DataPacket dp)
        {
            byte[] msg = dp.WriteToByteArr();

            pClient.Send(msg, msg.Length);
        }
    }
}
