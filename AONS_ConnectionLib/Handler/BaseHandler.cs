using AONS_ConnectionLib.Structures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace AONS_ConnectionLib
{
    public delegate void ReceivedMessageDel(DataPacket pDP);
    public delegate void ReceivedFileDel(string pFilepath);

    public abstract class BaseHandler
    {
        public const int PACKET_SIZE = 4096;
        //const int PACKET_SIZE = 100;

        #region Receiver
        public bool IsListenerStopped { get; protected set; } = true;
        private int _Port = 30000;
        public int Port
        {
            get => _Port;
            set
            {
                //Update Port and recreate listener
                _Port = value;
                CreateListener();
            }
        }
        public event ReceivedMessageDel? ReceivedMessageEvent;
        public event ReceivedFileDel? ReceivedFileEvent;

        protected bool _KeepListenerRunning = false;

        public string DownloadPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        #endregion

        #region Sender
        public IPAddress DestinationIP { get; set; } = null!;
        public int DestinationPort { get; set; } = 30000;

        public void SetDestination(string pDestIP, int pDestPort)
        {
            DestinationIP = IPAddress.Parse(pDestIP);
            DestinationPort = pDestPort;
        }
        #endregion

        #region ListenerHandling
        protected abstract object GetListener { get; }
        protected abstract void CreateListener();
        public virtual void StartListener()
        {
            if (GetListener == null)
                CreateListener();

            new TaskFactory().StartNew(() =>
            {
                IsListenerStopped = false;
                _KeepListenerRunning = true;

                DoListenerLoop();

                IsListenerStopped = true;
            });
        }
        protected abstract void DoListenerLoop();
        public abstract void StopListener();

        protected async Task ReadConnection(Stream pClientStream)
        {
            await new TaskFactory().StartNew(() =>
            {
                bool isFileTransmission = false;
                string? fileName = null;
                int curPart = 0;
                int mParts = 0;

                do
                {
                    var pDP = DataPacket.CreateDataPacket(pClientStream);
                    ReadConnection(pDP, ref fileName, out (bool isFile, int curPart, int maxPart) p);

                    //only the first package will help to see if the following packets will be part
                    //of a file transmission
                    if(p.isFile)
                        isFileTransmission = true;

                    curPart = p.curPart;
                    mParts = p.maxPart;
                } while (curPart < mParts);

                if (isFileTransmission)
                {
#pragma warning disable CS8604 // Possible null reference argument.
                    BuildFile(fileName);
#pragma warning restore CS8604 // Possible null reference argument.
                    ReceivedFileEvent?.Invoke(Path.Combine(DownloadPath, fileName));
                }
            });
        }

        protected void ReadConnection(DataPacket pDP, ref string? fileName, out (bool isFile, int curPart, int maxPart) p)
        {
            p.isFile = false;
            p.curPart = pDP.Part;
            p.maxPart = pDP.M_Part;

            if (pDP.PacketType == DataPacketType.File)
            {
                p.isFile = true;
                HandleFileTransmission(pDP, ref fileName);
            }
            else
                ReceivedMessageEvent?.Invoke(pDP);
        }

        /// <summary>
        /// sometimes (udp requests) you should not care what message type it is, handle
        /// everything as a plain message, if somebody really sends a file via udp he can
        /// handle the receiving himself too
        /// </summary>
        /// <param name="pDP"></param>
        protected void ReadConnection(DataPacket pDP)
        {
            ReceivedMessageEvent?.Invoke(pDP);
        }

        /// <summary>
        /// function to handle the receiving part of an file
        /// will take the Datapacket and either fill pFilename with the message, if prev. null
        /// or if filled will create a new file with the Datapacket data at the "DownloadPath" + "Filename" location
        /// </summary>
        /// <param name="pDP"></param>
        /// <param name="pFilename"></param>
        public void HandleFileTransmission(DataPacket pDP, ref string? pFilename)
        {
            //first message should contain a file name as message
            if (pFilename == null)
                pFilename = Encoding.Default.GetString(pDP.Data);
            else
            {
                using (FileStream fs = new FileStream(Path.Combine(DownloadPath, $"{pFilename}_part({pDP.Part}).tmp"), FileMode.Append, FileAccess.Write))
                {
                    fs.Write(pDP.Data, 0, pDP.Data.Length);
                    fs.Flush();
                }
            }
        }

        /// <summary>
        /// if given the same pFilename like "HandleFileTransmission" returned will fuse all "_part" files to one file 
        /// </summary>
        /// <param name="pFilename"></param>
        public void BuildFile(string pFilename)
        {
            using (FileStream fs = new FileStream(Path.Combine(DownloadPath, pFilename), FileMode.Create, FileAccess.Write))
            {
                foreach (var file in Directory.GetFiles(DownloadPath, $"{pFilename}_part(*).tmp")
                    .OrderBy(pX =>
                    {
                        int _Part = 0;
                        int startIdx = pX.LastIndexOf('(');
                        int destIdx = pX.LastIndexOf(')');
                        if (!int.TryParse(pX.Substring(startIdx + 1, destIdx - (startIdx + 1)), out _Part))
                            return -1;
                        return _Part;
                    }))
                {
                    using (FileStream fsRead = new FileStream(file, FileMode.Open, FileAccess.Read))
                    {
                        byte[] buffer = new byte[fsRead.Length];
                        fsRead.Read(buffer, 0, buffer.Length);
                        fs.Write(buffer, 0, buffer.Length);
                        fs.Flush();
                    }
                    File.Delete(file);
                }
            }
        }
        #endregion

        #region SendingHandling
        /// <summary>
        /// basic functionality to take a message and write it onto a stream
        /// </summary>
        /// <param name="pStream"></param>
        /// <param name="pPacketType"></param>
        /// <param name="pPart"></param>
        /// <param name="pMaxPart"></param>
        /// <param name="pData"></param>
        public static void SendMessage(Stream pStream, byte pPacketType, int pPart, int pMaxPart, byte[] pData)
        {
            SendMessage(pStream, new DataPacket { PacketType = (DataPacketType)pPacketType, Part = pPart, M_Part = pMaxPart, Data = pData });
        }

        /// <summary>
        /// basic functionality to take a DataPacket and write it onto a stream
        /// </summary>
        /// <param name="pStream"></param>
        /// <param name="pDP"></param>
        public static void SendMessage(Stream pStream, DataPacket pDP)
        {
            pDP.WriteToStream(pStream);
            pStream.Flush();
        }
        #endregion
    }
}
