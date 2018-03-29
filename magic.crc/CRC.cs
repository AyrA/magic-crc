using System;
using System.IO;
using System.Linq;

namespace magic.crc
{
    public static class CRC
    {
        /// <summary>
        /// Offset to us if the CRC is to be appended to the stream
        /// </summary>
        public const long OFFSET_EXPAND = -1;

        /// <summary>
        /// Gets the CRC 32 of a stream
        /// </summary>
        /// <param name="Source">Source Data</param>
        /// <returns>CRC32 value</returns>
        public static uint GetCRC(Stream Source)
        {
            return GetCrc32(Source);
        }

        /// <summary>
        /// Updates the CRC value of a stream
        /// </summary>
        /// <param name="Source">Data</param>
        /// <param name="NewCrc">New CRC value of Data</param>
        /// <param name="Offset">Offset where the new value will be written to, -1 to append to stream, this will pad the stream with 4x zero bytes</param>
        /// <remarks>Stream must be writable and seekable for this to work</remarks>
        public static void UpdateCRC(Stream Source, uint NewCrc = uint.MaxValue, long Offset = OFFSET_EXPAND)
        {
            var Magic = GetMagicCRC(Source, NewCrc, Offset);
            if (Offset == OFFSET_EXPAND)
            {
                Offset = Source.Length - 4;
            }
            Source.Seek(Offset, SeekOrigin.Begin);
            for (int i = 0; i < 4; i++)
            {
                byte current = (byte)Source.ReadByte();
                Source.Seek(-1, SeekOrigin.Current);
                current ^= (byte)((ReverseBits(Magic) >> (i * 8)) & 0xFF);
                Source.WriteByte(current);
            }
        }

        /// <summary>
        /// Gets the new CRC Value of a stream according to location and NewCrc Value
        /// </summary>
        /// <param name="Source">Byte array to change CRC</param>
        /// <param name="NewCrc">New CRC Value</param>
        /// <param name="Offset">Offset where the new value will be written to, -1 to append to stream, this will pad the stream with 4x zero bytes</param>
        /// <remarks>
        /// This CRC sum can't be directly written to stream.
        /// Use <see cref="UpdateCRC(Stream, uint, long)"/> to write the new CRC value properly.
        /// </remarks>
        /// <returns>New CRC Value</returns>
        public static uint GetMagicCRC(Stream Source, uint NewCrc = uint.MaxValue, long Offset = OFFSET_EXPAND)
        {
            uint delta = 0;

            if (Source == null || Source.Length == 0)
            {
                throw new ArgumentNullException("Source");
            }
            if (Offset > Source.Length - 4 || Offset < OFFSET_EXPAND)
            {
                throw new ArgumentOutOfRangeException("Offset", "Ranges: -1 - " + Source.Length);
            }
            //if less than zero, expand stream to append checksum
            if (Offset == OFFSET_EXPAND)
            {
                Source.Seek(0, SeekOrigin.End);
                Offset = Source.Length;
                Source.Write(new byte[4], 0, 4);
                Source.Seek(0, SeekOrigin.Begin);
            }
            var OldCrc = BitConverter.GetBytes(GetCrc32(Source)).Reverse().ToArray();
            delta = ReverseBits(NewCrc ^ BitConverter.ToUInt32(OldCrc, 0));
            delta = (uint)MultiplyMod(ReciprocalMod(PowMod(2, (ulong)(Source.Length - Offset) * 8UL)), delta);
            return delta;
            //return NewCrc;
        }

        #region Utilities

        /// <summary>
        /// Used by various Polynomial functions
        /// </summary>
        private const ulong POLYNOMIAL = 0x104C11DB7UL;

        /// <summary>
        /// Calculates the CRC32 of a stream
        /// </summary>
        /// <param name="f">Data</param>
        /// <remarks>This will not seek the stream and just reads to the end</remarks>
        /// <returns>CRC32</returns>
        private static uint GetCrc32(Stream f)
        {
            Crc32 c = new Crc32();
            return BitConverter.ToUInt32(c.ComputeHash(f), 0);
        }

        /// <summary>
        /// Reverses the bits in an integer
        /// </summary>
        public static uint ReverseBits(uint x)
        {
            uint result = 0;
            int i;
            for (i = 0; i < 32; i++)
            {
                result = (result << 1) | ((x >> i) & 1);
            }
            return result;
        }

        #endregion

        #region Polynominal arithmetic

        /// <summary>
        /// Returns polynomial x multiplied by polynomial y modulo the generator polynomial.
        /// </summary>
        private static ulong MultiplyMod(ulong x, ulong y)
        {
            // Russian peasant multiplication algorithm
            ulong z = 0;
            while (y != 0)
            {
                z ^= x * (y & 1UL);
                y >>= 1;
                x <<= 1;
                if ((x & 0x100000000UL) != 0)
                {
                    x ^= POLYNOMIAL;
                }
            }
            return z;
        }


        /// <summary>
        /// Returns polynomial x to the power of natural number y modulo the generator polynomial.
        /// </summary>
        private static ulong PowMod(ulong x, ulong y)
        {
            // Exponentiation by squaring
            ulong z = 1;
            while (y != 0)
            {
                if ((y & 1) != 0)
                    z = MultiplyMod(z, x);
                x = MultiplyMod(x, x);
                y >>= 1;
            }
            return z;
        }

        /// <summary>
        /// Computes polynomial x divided by polynomial y, returning the quotient and remainder.
        /// </summary>
        /// <param name="x">Polynomial 1</param>
        /// <param name="y">Polynomial 2</param>
        /// <param name="q">Quotient</param>
        /// <param name="r">Remainder</param>
        private static void DivideAndRemainder(ulong x, ulong y, out ulong q, out ulong r)
        {
            if (y == 0)
            {
                throw new ArgumentException("Division by zero", "y");
            }
            if (x == 0)
            {
                q = 0;
                r = 0;
                return;
            }

            int ydeg = GetDegree(y);
            ulong z = 0;
            int i;
            for (i = GetDegree(x) - ydeg; i >= 0; i--)
            {
                if ((x & (1UL << (i + ydeg))) != 0)
                {
                    x ^= y << i;
                    z |= 1UL << i;
                }
            }
            q = z;
            r = x;
        }

        /// <summary>
        /// Returns the reciprocal of polynomial x with respect to the generator polynomial.
        /// </summary>
        /// <param name="x">Polynomial</param>
        private static ulong ReciprocalMod(ulong x)
        {
            ulong y = x;
            x = POLYNOMIAL;
            ulong a = 0;
            ulong b = 1;
            while (y != 0)
            {
                ulong q, r;
                DivideAndRemainder(x, y, out q, out r);
                ulong c = a ^ MultiplyMod(q, b);
                x = y;
                y = r;
                a = b;
                b = c;
            }
            if (x == 1)
            {
                return a;
            }
            else
            {
                throw new Exception("Reciprocal does not exist\n");
            }
        }

        private static int GetDegree(ulong x)
        {
            int result = -1;
            while (x != 0)
            {
                x >>= 1;
                ++result;
            }
            return result;
        }

        #endregion
    }
}
