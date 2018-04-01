using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace magic.crc
{
    public class Program
    {
        private struct ERR
        {
            public const int SUCCESS = 0;
            public const int NOARG = SUCCESS + 1;
            public const int PARAMS = NOARG + 1;
            public const int INVALIDSIZE = PARAMS + 1;
            public const int FILE_NOT_FOUND = INVALIDSIZE + 1;
            public const int IO_ERROR = FILE_NOT_FOUND + 1;
        }

        private struct CMD
        {
            public long Offset;
            public uint CRC;
            public string InFile;
            public string OutFile;
            public bool Valid;
        }

        public static int Main(string[] args)
        {
#if DEBUG
            args = new string[]
            {
                @"C:\Temp\crc.bin",
                @"C:\Temp\magic-crc-2.bin",
                "/O", "-4"
            };
#endif

            if (args.Length == 0 || IsHelp(args))
            {
                ShowHelp();
                return ERR.NOARG;
            }
            var Parsed = GetArgs(args);
            if (Parsed.Valid)
            {
                if (!File.Exists(Parsed.InFile))
                {
                    Console.Error.WriteLine("Input file not found");
                    return ERR.FILE_NOT_FOUND;
                }
                bool IsSameFile = string.IsNullOrEmpty(Parsed.OutFile) || Tools.ComparePath(Parsed.InFile, Parsed.OutFile);
                FileStream IN = null;
                try
                {
                    IN = File.Open(Parsed.InFile, FileMode.Open, IsSameFile ? FileAccess.ReadWrite : FileAccess.Read, IsSameFile ? FileShare.None : FileShare.Read);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("Unable to read input file. Details: {0}", ex.Message);
                    return ERR.IO_ERROR;
                }
                using (IN)
                {
                    //Offset is larger than possible
                    if (Parsed.Offset > IN.Length - 4)
                    {
                        Console.Error.WriteLine("Offset too large. Maximum is {0} (Filesize - 4)", IN.Length - 4);
                        return ERR.INVALIDSIZE;
                    }
                    //Offset smaller than possible (before file start)
                    if (Parsed.Offset < 0 && Parsed.Offset != CRC.OFFSET_EXPAND && IN.Length - Parsed.Offset < 0)
                    {
                        Console.Error.WriteLine("Offset too small. Minimum is {0} (0 - Filesize)", -IN.Length);
                        return ERR.INVALIDSIZE;
                    }
                    //Offset negative but not negative enough
                    if (Parsed.Offset > -4 && Parsed.Offset < 0 && Parsed.Offset != CRC.OFFSET_EXPAND)
                    {
                        Console.Error.WriteLine("Negative offsets must be -4 or less. Given: ", Parsed.Offset);
                        return ERR.INVALIDSIZE;
                    }
                    if (Parsed.Offset < 0 && Parsed.Offset != CRC.OFFSET_EXPAND)
                    {
                        //Don't subtract. Offset is already negative.
                        Parsed.Offset = IN.Length + Parsed.Offset;
                    }
                    //Inline update of CRC
                    if (IsSameFile)
                    {
                        Console.Error.WriteLine("Updating CRC to 0x{0:X8}", Parsed.CRC);
                        CRC.UpdateCRC(IN, Parsed.CRC, Parsed.Offset);
                        Console.Error.WriteLine("Done");
                    }
                    else
                    {
                        //Copying file before changing it
                        Console.Error.WriteLine("Copying file...");
                        File.Copy(Parsed.InFile, Parsed.OutFile, true);
                        Console.Error.WriteLine("Updating CRC to 0x{0:X8}", Parsed.CRC);
                        using (var OUT = File.Open(Parsed.OutFile, FileMode.Open, FileAccess.ReadWrite))
                        {
                            CRC.UpdateCRC(OUT, Parsed.CRC, Parsed.Offset);
                        }
                    }
                }
                return ERR.SUCCESS;
            }
            return ERR.PARAMS;
        }

        private static bool IsHelp(string[] args)
        {
            var Helps = "/?,-?,--help".Split(',');
            return args.Any(m => Helps.Contains(m.ToUpper()));
        }

        private static CMD GetArgs(string[] args)
        {
            CMD C = new CMD();
            //Defaults
            C.Valid = true;
            C.Offset = CRC.OFFSET_EXPAND;
            C.CRC = uint.MaxValue;

            for (var i = 0; i < args.Length && C.Valid; i++)
            {
                switch (args[i].ToUpper())
                {
                    case "/C":
                        if (args.Length > i + 1)
                        {
                            if (Tools.IsValidCrc32(args[i + 1]))
                            {
                                C.CRC = Tools.HexToDec(args[++i]);
                            }
                            else
                            {
                                C.Valid = false;
                                Console.Error.WriteLine("The value '{0}' is not a valid CRC32 sum", args[i + 1]);
                            }
                        }
                        else
                        {
                            C.Valid = false;
                            Console.Error.WriteLine("/C must be followed by a hexadecimal number");
                        }
                        break;
                    case "/O":
                        if (args.Length > i + 1)
                        {
                            C.Offset = Tools.LongOrDefault(args[i + 1], long.MinValue);
                            if (C.Offset == long.MinValue)
                            {
                                C.Valid = false;
                                Console.Error.WriteLine("Invalid Value for offset. Expected number, got '{0}'", args[i + 1]);
                            }
                            else
                            {
                                ++i;
                            }
                        }
                        else
                        {
                            C.Valid = false;
                            Console.Error.WriteLine("/O must be followed by a number");
                        }
                        break;
                    default:
                        if (string.IsNullOrWhiteSpace(C.InFile))
                        {
                            C.InFile = args[i];
                        }
                        else if (string.IsNullOrWhiteSpace(C.OutFile))
                        {
                            C.OutFile = args[i];
                        }
                        else
                        {
                            Console.Error.WriteLine("Unexpected Argument: '{0}'", args[i]);
                            C.Valid = false;
                        }
                        break;
                }
            }

            C.Valid &= !string.IsNullOrEmpty(C.InFile);

            return C;
        }

        private static void ShowHelp()
        {
            Write(Console.Error, @"magic-crc [/O offset] [/C CRC] <input> [output]
Calculates a Magic CRC-32 Sum

/C CRC     - New CRC-32 sum as hex value. If not specified, 0xFFFFFFFF is used. The Prefix '0x' is optional.
/O offset  - Offset of the 4 bytes to change. Positive numbers offset from the start, negative numbers from the end. If this argument is not specified, the new bytes are appended to the file.
input      - File whose CRC sum is to change
output     - Location to write new file to. If not specified, the source file will be overwritten.", Console.BufferWidth - 1);
        }

        private static void Write(TextWriter Output, string Text, int LineLength)
        {
            char[] Spaces = Unicode.Get(UnicodeCategory.SpaceSeparator);
            char[] LineBreaks = Unicode.Get(UnicodeCategory.ParagraphSeparator)
                .Concat(Unicode.Get(UnicodeCategory.LineSeparator))
                .Concat(Unicode.Get(UnicodeCategory.Control))
                .ToArray();
            var Lines = Text.Replace("\r\n", "\n").Split(LineBreaks);
            foreach (var Line in Lines)
            {
                var LinePos = 0;
                var Words = Line.Split(Spaces);
                foreach (var Word in Words)
                {
                    if (Word.Length > LineLength)
                    {
                        if (LinePos > 0)
                        {
                            Output.WriteLine();
                        }
                        Output.WriteLine(Word.Substring(0, LineLength - 4) + "...");
                        LinePos = 0;
                    }
                    else if (LinePos + Word.Length < LineLength)
                    {
                        Output.Write("{0} ", Word);
                        LinePos += Word.Length + 1;
                    }
                    else
                    {
                        Output.WriteLine();
                        Output.Write("{0} ", Word);
                        LinePos = Word.Length + 1;
                    }
                }
                Output.WriteLine();
            }
        }
    }
}
