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
            Data = Encoding.Default.GetBytes(pData);
        }

        public void WriteToStream(Stream pStream)
        {
            CreateXML_AsStream(pStream, this);
        }

        public static DataPacket CreateDataPacket(Stream pStream)
        {
            DataPacket packet = new DataPacket();
            List<byte> msg = new List<byte>();
            int curByte = 0;
            byte pipeCount = 0;
            int pDataLength = 0;

            //Step 1, read overhead
            while (pipeCount < 4) //0 - PacketType, 1 - Part, 2 - Max Part, 3 - DataLength, 4 - Data
            {
                curByte = pStream.ReadByte();
                if (curByte == -1) break; // -1 means end of stream reached
                msg.Add((byte)curByte);
                if (curByte == (byte)'|')
                    pipeCount++;
            }

            //Step 2, write overhead into corresponding variables
            string[] splits = Encoding.Default.GetString(msg.ToArray()).Split('|');
            packet.PacketType = (DataPacketType)byte.Parse(splits[0]);
            packet.Part = int.Parse(splits[1]);
            packet.M_Part = int.Parse(splits[2]);
            pDataLength = int.Parse(splits[3]);

            //Step 3, read the real data with the given overhead length
            packet.Data = new byte[pDataLength];
            pStream.Read(packet.Data, 0, pDataLength);

            return packet;
        }

        public override string ToString()
        {
            return CreateXML_AsString(this);
        }

        public static string CreateXML_AsString(DataPacket pDP)
        {
            using (Stream ms = CreateXML_AsStream(pDP))
            {
                StreamReader reader = new StreamReader(ms);
                ms.Position = 0;
                return reader.ReadToEnd();
            }
        }

        public static Stream CreateXML_AsStream(DataPacket pDP)
        {
            Stream ret = new MemoryStream();
            CreateXML_AsStream(ret, pDP);
            return ret;
        }

        public static void CreateXML_AsStream(Stream pStream, DataPacket pDP)
        {
            byte[] buffer = Encoding.Default.GetBytes($"{(int)pDP.PacketType}|{pDP.Part}|{pDP.M_Part}|{pDP.Data.Length}|");
            int origLength = buffer.Length;
            Array.Resize(ref buffer, buffer.Length + pDP.Data.Length);
            Array.Copy(pDP.Data, 0, buffer, origLength, pDP.Data.Length);
            pStream.Write(buffer, 0, buffer.Length); //Write the overhad first, with the information on part and data length and the data. All in one stream to avoid weird split issues
            pStream.Flush();
        }
    }
}
