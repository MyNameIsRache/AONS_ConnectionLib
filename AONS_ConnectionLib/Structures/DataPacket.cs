using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;

using System.Text.Json;

namespace AONS_ConnectionLib.Structures
{
    public class DataPacketReadException : Exception
    {
        public DataPacketReadException() : base() { }
        public DataPacketReadException(string message) : base(message) { }
    }

    public enum DataPacketType : byte
    {
        Undefined = 0,
        Message = 1,
        File = 2
    }

    public class DataPacket
    {
        //{"DP":{"Type":,"Part":,"MPart":,"Data":""}} => 43
        public const byte OVERHEAD_SIZE = 43;

        public DataPacketType PacketType { get; set; } = DataPacketType.Undefined;
        public int Part { get; set; } = 0;
        public int M_Part { get; set; } = 0;
        public byte[] Data { get; set; }

        public DataPacket()
        {
            Part = 1;
            M_Part = 1;
            Data = new byte[0];
        }

        public DataPacket(byte[] pData) : this()
        {
            Data = pData;
        }

        public DataPacket(string pData) : this()
        {
            PacketType = DataPacketType.Message;
            Part = 1;
            M_Part = 1;
            Data = Encoding.Default.GetBytes(pData);
        }

        public void WriteToStream(Stream pStream)
        {
            CreateMessage_AsStream(pStream, this);
        }

        public byte[] WriteToByteArr()
        {
            return CreateMessage_AsByteArr(this);
        }

        private static DataPacket CreateDataPacket(string[] pOverhead, byte[] msg)
        {
            DataPacket packet = new DataPacket();
            packet.PacketType = (DataPacketType)byte.Parse(pOverhead[0]);
            packet.Part = int.Parse(pOverhead[1]);
            packet.M_Part = int.Parse(pOverhead[2]);

            packet.Data = msg;

            return packet;
        }

        /// <summary>
        /// Create a Datapacket with the message byte array
        /// </summary>
        /// <param name="pMessage"></param>
        /// <returns></returns>
        public static DataPacket CreateDataPacket(byte[] pMessage)
        {
            int idx = 0;

            //Step 1, read overhead
            for (int pipeCount = 0, lng = pMessage.Length; pipeCount < 4 && idx < lng; idx++)  //0 - PacketType, 1 - Part, 2 - Max Part, 3 - DataLength, 4 - Data
            {
                if (pMessage[idx] == (byte)'|')
                    pipeCount++;
            }

            //Step 2, split off the message in overhead and message
            //todo: have to actually test if the dimensions are correct
            byte[] _overhead = new byte[idx];
            Array.Copy(pMessage, _overhead, idx);
            int msgSize = pMessage.Length - idx;
            byte[] _msg = new byte[msgSize];
            Array.Copy(pMessage, idx, _msg, 0, msgSize);

            //Step 3, create the datapacket with the help of the arrays
            return CreateDataPacket(Encoding.Default.GetString(_overhead).Split('|'), _msg);
        }

        /// <summary>
        /// Create a Datapacket with a stream
        /// </summary>
        /// <param name="pStream"></param>
        /// <returns></returns>
        public static DataPacket CreateDataPacket(Stream pStream)
        {
            List<byte> msg = new List<byte>();
            int curByte = 0;
            byte pipeCount = 0;

            //Step 1, read overhead
            while (pipeCount < 4) //0 - PacketType, 1 - Part, 2 - Max Part, 3 - DataLength, 4 - Data
            {
                curByte = pStream.ReadByte();
                if (curByte == -1) break; // -1 means end of stream reached
                msg.Add((byte)curByte);
                if (curByte == (byte)'|')
                    pipeCount++;
            }

            //Step 2, split overhead to read corresponding datalength
            string[] splits = Encoding.Default.GetString(msg.ToArray()).Split('|');
            int pDataLength = int.Parse(splits[3]);

            //Step 3, read the real data with the given overhead length
            byte[] pData = new byte[pDataLength];
            pStream.Read(pData, 0, pDataLength);

            //Step 4, create the datapacket for further usage
            return CreateDataPacket(splits, pData);
        }

        public override string ToString()
        {
            return CreateXML_AsString(this);
        }

        public static string CreateXML_AsString(DataPacket pDP)
        {
            using (Stream ms = CreateMessage_AsStream(pDP))
            {
                StreamReader reader = new StreamReader(ms);
                ms.Position = 0;
                return reader.ReadToEnd();
            }
        }

        public static Stream CreateMessage_AsStream(DataPacket pDP)
        {
            Stream ret = new MemoryStream();
            CreateMessage_AsStream(ret, pDP);
            return ret;
        }

        private static byte[] CreateMessage_Overhead(DataPacket pDP)
        {
            return Encoding.Default.GetBytes($"{(int)pDP.PacketType}|{pDP.Part}|{pDP.M_Part}|{pDP.Data.Length}|");
        }

        public static byte[] CreateMessage_AsByteArr(DataPacket pDP)
        {
            List<byte> bArr = new List<byte>(CreateMessage_Overhead(pDP));
            bArr.AddRange(pDP.Data);
            return bArr.ToArray();
        }

        public static void CreateMessage_AsStream(Stream pStream, DataPacket pDP)
        {
            byte[] buffer = CreateMessage_Overhead(pDP);
            int origLength = buffer.Length;
            Array.Resize(ref buffer, buffer.Length + pDP.Data.Length);
            Array.Copy(pDP.Data, 0, buffer, origLength, pDP.Data.Length);
            pStream.Write(buffer, 0, buffer.Length); //Write the overhad first, with the information on part and data length and the data. All in one stream to avoid weird split issues
            pStream.Flush();
        }
    }
}
