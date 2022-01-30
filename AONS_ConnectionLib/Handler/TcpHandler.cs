using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;

using AONS_ConnectionLib.Structures;
using AONS_ConnectionLib.Structures.Exceptions;
using System.Text;

namespace AONS_ConnectionLib
{
    public class TcpHandler : BaseHandler, IDisposable
    {
        TcpListener? _Listener;

#pragma warning disable CS8764 // Nullability of return type doesn't match overridden member (possibly because of nullability attributes).
        protected override object? GetListener => _Listener;
#pragma warning restore CS8764 // Nullability of return type doesn't match overridden member (possibly because of nullability attributes).

        private TcpHandler()
        {

        }

        public TcpHandler(int pPort = 30000)
        {
            Port = pPort;

            CreateListener();
        }

        protected override void CreateListener()
        {
            _Listener = new TcpListener(IPAddress.Any, Port);
        }

        protected override void DoListenerLoop()
        {
            _KeepListenerRunning = true;
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            _Listener.Start();
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            IsListenerStopped = false;

            //try catch in case the listener was stopped
            //but this function still tried to call the pending function
            try
            {
                while (_KeepListenerRunning)
                {
                    if (_Listener.Pending())
                        HandleTcpClient(_Listener.AcceptTcpClient());
                    else
                        Thread.Sleep(100);
                }
            }
            catch { }
            _Listener.Stop();
        }

        /// <summary>
        /// await the reading of the connection and dispose the client afterwards
        /// </summary>
        /// <param name="pClient"></param>
        protected async void HandleTcpClient(TcpClient pClient)
        {
            await ReadConnection(pClient.GetStream());
            pClient.Dispose();
        }

        public override void StopListener()
        {
            _KeepListenerRunning = false;
        }

        public void SendMessage(DataPacketType pPacketType, int pPart, int pMPart, byte[] pData)
        {
            SendMessage((byte)pPacketType, pPart, pMPart, pData);
        }

        public void SendMessage(byte pPacketType, int pPart, int pMPart, byte[] pData)
        {
            if (DestinationIP == null) throw new NoDestinationException("No Destination IP set");

            SendMessage(new IPEndPoint(DestinationIP, DestinationPort), pPacketType, pPart, pMPart, pData);
        }

        public static TcpClient CreateConnection(IPAddress pIpAddress, int pPort)
        {
            return CreateConnection(new IPEndPoint(pIpAddress, pPort));
        }

        public static TcpClient CreateConnection(IPEndPoint pEndPoint)
        {
            TcpClient ret = new TcpClient();
            ret.Connect(pEndPoint);
            return ret;
        }

        public static void SendMessage(string pDestIP, int pDestPort, string pMessage)
        {
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
            if (IPAddress.TryParse(pDestIP, out IPAddress pAddress))
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
                SendMessage(pAddress, pDestPort, pMessage);
            else
                throw new IPInvalidException($"Was not able to parse {pDestIP}");
        }

        public static void SendMessage(IPAddress pDestIP, int pDestPort, string pMessage)
        {
            SendMessage(new IPEndPoint(pDestIP, pDestPort), pMessage);
        }

        public static void SendMessage(IPEndPoint pDest, string pMessage)
        {
            SendMessage(pDest, Encoding.Default.GetBytes(pMessage));
        }

        public static void SendMessage(string pDestIP, int pDestPort, byte[] pData)
        {
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
            if (IPAddress.TryParse(pDestIP, out IPAddress pAddress))
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
                SendMessage(pAddress, pDestPort, pData);
        }

        public static void SendMessage(IPAddress pDestIP, int pDestPort, byte[] pData)
        {
            SendMessage(new IPEndPoint(pDestIP, pDestPort), pData);
        }

        public static void SendMessage(IPEndPoint pDest, byte[] pData)
        {
            using (TcpClient _Client = CreateConnection(pDest))
            {
                //todo: have to calculate the message size here as well and split it into parts in case the message is too big
                SendMessage(_Client, DataPacketType.Message, 1, 1, pData);
            }
        }

        public static void SendMessage(string pDestIP, int pDestPort, DataPacketType pPacketType, int pPart, int pMaxPart, byte[] pData)
        {
            SendMessage(pDestIP, pDestPort, (byte)pPacketType, pPart, pMaxPart, pData);
        }

        public static void SendMessage(string pDestIP, int pDestPort, byte pPacketType, int pPart, int pMaxPart, byte[] pData)
        {
            SendMessage(new IPEndPoint(IPAddress.Parse(pDestIP), pDestPort), pPacketType, pPart, pMaxPart, pData);
        }

        public static void SendMessage(IPEndPoint pEndpoint, byte pPacketType, int pPart, int pMaxPart, byte[] pData)
        {
            using (TcpClient _Client = CreateConnection(pEndpoint))
            {
                SendMessage(_Client, pPacketType, pPart, pMaxPart, pData);
            }
        }

        public static void SendMessage(TcpClient pClient, DataPacketType pPacketType, int pPart, int pMaxPart, byte[] pData)
        {
            SendMessage(pClient, (byte)pPacketType, pPart, pMaxPart, pData);
        }

        public static void SendMessage(TcpClient pClient, byte pPacketType, int pPart, int pMaxPart, byte[] pData)
        {
            var ns = pClient.GetStream();
            SendMessage(ns, pPacketType, pPart, pMaxPart, pData);
        }

        public static bool SendFile(string pDestIP, int pDestPort, string pFileName)
        {
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
            if (IPAddress.TryParse(pDestIP, out IPAddress pAddress))
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
                return SendFile(pAddress, pDestPort, pFileName);
            return false;
        }

        public static bool SendFile(IPAddress pDestIP, int pDestPort, string pFileName)
        {
            return SendFile(new IPEndPoint(pDestIP, pDestPort), pFileName);
        }

        public static bool SendFile(IPEndPoint pDest, string pFileName)
        {
            using (TcpClient _Client = CreateConnection(pDest))
            {
                return SendFile(_Client, pFileName);
            }
        }

        public static bool SendFile(TcpClient pClient, string pFileName)
        {
            if (!File.Exists(pFileName))
                return false;

            using (FileStream fs = new FileStream(pFileName, FileMode.Open, FileAccess.Read))
            {
                FileInfo fi = new FileInfo(pFileName);
                int mParts = (int)Math.Ceiling(fi.Length / (double)PACKET_SIZE); //to calculate the part count i would need to know how big the buffer is, but to know how long the buffer is i would need to know how many char i have to subtract for the max part count :D
                int currentPart = 1;
                //buffer has to be adjusted so that the PACKET_SIZE is reached with the overhead and the core data
                //therefore I have to subtract the json overhead and individual value  v  lengths for type, max parts and part
                int bufferSize = PACKET_SIZE - DataPacket.OVERHEAD_SIZE - mParts.ToString().Length - 2; // 2 == FileType + CurrentPart which both shouldn't be > 10 at this point
                byte[] buffer = new byte[bufferSize];

                SendMessage(pClient, DataPacketType.File, 0, mParts, Encoding.Default.GetBytes(fi.Name));

                while (fs.Position < fi.Length)
                {
                    Array.Clear(buffer);

                    //the current part is now 2 in length, have to adjust buffer size
                    if (currentPart == 10)
                        Array.Resize(ref buffer, PACKET_SIZE - DataPacket.OVERHEAD_SIZE - mParts.ToString().Length - 3);

                    //have to calculate with buffer length, since it's not a given that the PACKET_SIZE will work here
                    if (fi.Length - (fs.Position + buffer.Length) < 0)
                        Array.Resize(ref buffer, (int)(buffer.Length - (fs.Position + buffer.Length - fi.Length)));

                    fs.Read(buffer, 0, buffer.Length);
                    SendMessage(pClient, DataPacketType.File, currentPart++, mParts, buffer);
                }
            }

            return true;
        }

        public void Dispose()
        {
            if (_Listener != null)
                _Listener.Stop();
        }
    }
}