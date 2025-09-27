using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NicoNamaRokuga.Message
{
    public class BinaryStream
    {
        private List<byte> buffer;
        private int offset;

        public BinaryStream()
        {
            buffer = new List<byte>();
            offset = 0;
        }

        public void AddBuffer(BinaryStream buf, byte[] data)
        {
            buf.buffer.AddRange(data);
        }

        public void ClearBuffer(BinaryStream buf)
        {
            buf.buffer.Clear();
            buf.offset = 0;
        }

        private VarintResult DecodeVarint(BinaryStream buf, int t)
        {
            int a = 0;
            int o = buf.buffer.Count;
            int i = 0;

            while (true)
            {
                if (o <= t)
                {
                    return null;
                }

                byte b = buf.buffer[t];
                bool r = (b & 0x80) != 0;
                a |= (b & 0x7F) << i;

                if (r)
                {
                    t++;
                    i += 7;
                }
                else
                {
                    break;
                }
            }

            return new VarintResult { Value = a, Offset = t };
        }

        public IEnumerable<byte[]> Read(BinaryStream buf)
        {
            int currentOffset = buf.offset;

            while (true)
            {
                var e = DecodeVarint(buf, currentOffset);
                if (e == null)
                {
                    yield break;
                }

                int value = e.Value;
                int newOffset = e.Offset;
                int start = newOffset + 1;
                int rEnd = start + value;

                if (buf.buffer.Count < rEnd)
                {
                    yield break;
                }

                currentOffset = rEnd;
                buf.offset = rEnd;

                if (rEnd - start > 0)
                {
                    byte[] binaryData = buf.buffer.GetRange(start, rEnd - start).ToArray();
                    yield return binaryData;
                }
                else
                {
                    yield break;
                }
            }
        }

        private class VarintResult
        {
            public int Value { get; set; }
            public int Offset { get; set; }
        }
    }

}
