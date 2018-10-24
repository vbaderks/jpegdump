// Copyright (c) Victor Derks. All rights reserved. See the accompanying "LICENSE.md" for licensed use.

using System;
using System.IO;

namespace JpegDump
{
    internal enum JpegMarker
    {
        StartOfImage = 0xD8, // SOI
        EndOfImage = 0xD9,   // EOI
        StartOfScan = 0xDA,   // SOS
        StartOfFrameJpegLS = 0xF7,                  // SOF_55: Marks the start of a (JPEG-LS) encoded frame.
        JpegLSExtendedParameters = 0xF8,            // LSE:    JPEG-LS extended parameters.
        ApplicationData0 = 0xE0,                    // APP0: Application data 0: used for JFIF header.
        ApplicationData7 = 0xE7,                    // APP7: Application data 7: color space.
        ApplicationData8 = 0xE8,                    // APP8: Application data 8: colorXForm.
        Comment = 0xFE                              // COM:  Comment block.
    }

    internal class JpegStreamReader
    {
        private readonly BinaryReader reader;
        private bool jpegLSStream;

        public JpegStreamReader(Stream stream)
        {
            reader = new BinaryReader(stream);
        }

        public void Dump()
        {
            int c;
            while ((c = reader.BaseStream.ReadByte()) != -1)
            {
                if (c == 0xFF)
                {
                    int markerCode = reader.BaseStream.ReadByte();
                    if (IsMarkerCode(markerCode))
                    {
                        DumpMarker(markerCode);
                    }
                }
            }
        }

        private bool IsMarkerCode(int code)
        {
            // To prevent marker codes in the encoded bit stream encoders must encode the next byte zero or the next bit zero (jpeg-ls).
            if (jpegLSStream)
                return (code & 0x80) == 0X80;

            return code > 0;
        }

        private void DumpMarker(int markerCode)
        {
            //  FFD0 to FFD9 and FF01, markers without size.

            switch ((JpegMarker) markerCode)
            {
                case JpegMarker.StartOfImage:
                    DumpStartOfImageMarker();
                    break;

                case JpegMarker.EndOfImage:
                    Console.WriteLine("{0:D8} Marker 0xFFD9. EOI (End Of Image), defined in ITU T.81/IEC 10918-1", GetStartOffset());
                    break;

                case JpegMarker.StartOfFrameJpegLS:
                    jpegLSStream = true;
                    DumpStartOfFrameJpegLS();
                    break;

                case JpegMarker.StartOfScan:
                    DumpStartOfScan();
                    break;

                case JpegMarker.ApplicationData7:
                    DumpApplicationData7();
                    break;

                case JpegMarker.ApplicationData8:
                    DumpApplicationData8();
                    break;

                default:
                    Console.WriteLine("{0:D8} Marker 0xFF{1:X}", GetStartOffset(), markerCode);
                    break;
            }
        }

        private long GetStartOffset()
        {
            return reader.BaseStream.Position - 2;
        }

        private long Position => reader.BaseStream.Position;


        private void DumpStartOfImageMarker()
        {
            Console.WriteLine("{0:D8} Marker 0xFFD8: SOI (Start Of Image), defined in ITU T.81/IEC 10918-1", GetStartOffset());
        }

        private void DumpStartOfFrameJpegLS()
        {
            Console.WriteLine("{0:D8} Marker 0xFFF7: SOF_55 (Start Of Frame Jpeg-LS), defined in ITU T.87/IEC 14495-1 JPEG LS", GetStartOffset());
            Console.WriteLine("{0:D8}  Size = {1}", Position, ReadUInt16BigEndian());
            Console.WriteLine("{0:D8}  Sample precision (P) = {1}", Position, reader.ReadByte());
            Console.WriteLine("{0:D8}  Number of lines (Y) = {1}", Position, ReadUInt16BigEndian());
            Console.WriteLine("{0:D8}  Number of samples per line (X) = {1}", Position, ReadUInt16BigEndian());
            var componentCount = reader.ReadByte();
            Console.WriteLine("{0:D8}  Number of image components in a frame (Nf) = {1}", Position, componentCount);
            for (int i = 0; i < componentCount; i++)
            {
                Console.WriteLine("{0:D8}   Component identifier (Ci) = {1}", Position, reader.ReadByte());
                Console.WriteLine("{0:D8}   H and V sampling factor (Hi + Vi) = {1}", Position, reader.ReadByte());
                Console.WriteLine("{0:D8}   Quantization table (Tqi) [reserved, should be 0] = {1}", Position, reader.ReadByte());
            }
        }

        private void DumpStartOfScan()
        {
            Console.WriteLine("{0:D8} Marker 0xFFDA: SOS (Start Of Scan), defined in ITU T.81/IEC 10918-1", GetStartOffset());
            Console.WriteLine("{0:D8}  Size = {1}", Position, ReadUInt16BigEndian());
            var componentCount = reader.ReadByte();
            Console.WriteLine("{0:D8}  Component Count = {1}", Position, componentCount);
            for (int i = 0; i < componentCount; i++)
            {
                Console.WriteLine("{0:D8}   Component identifier (Ci) = {1}", Position, reader.ReadByte());
                Console.WriteLine("{0:D8}   Table? (?) = {1}", Position, reader.ReadByte());
            }

            Console.WriteLine("{0:D8}  Allowed lossy error (?) = {1}", Position, reader.ReadByte());
            Console.WriteLine("{0:D8}  Interleave mode (?) = {1}", Position, reader.ReadByte());
            Console.WriteLine("{0:D8}  Transformation (?) = {1}", Position, reader.ReadByte());
        }

        private void DumpApplicationData7()
        {
            Console.WriteLine("{0:D8} Marker 0xFFE7: APP7 (Application Data 7), defined in ITU T.81/IEC 10918-1", GetStartOffset());
            int size = ReadUInt16BigEndian();
            Console.WriteLine("{0:D8}  Size = {1}", Position, size);
            for (int i = 0; i < size - 2; i++)
            {
                reader.ReadByte();
            }
        }

        private void DumpApplicationData8()
        {
            Console.WriteLine("{0:D8} Marker 0xFFE8: APP8 (Application Data 8), defined in ITU T.81/IEC 10918-1", GetStartOffset());
            int size = ReadUInt16BigEndian();
            Console.WriteLine("{0:D8}  Size = {1}", Position, size);
            for (int i = 0; i < size - 2; i++)
            {
                reader.ReadByte();
            }
        }

        private ushort ReadUInt16BigEndian()
        {
            return (ushort)((reader.ReadByte() << 8) | reader.ReadByte());
        }
    }


    public static class Program
    {
        private static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: jpegdump <filename>");
                return;
            }

            try
            {
                using (var stream = new FileStream(args[0], FileMode.Open))
                {
                    Console.WriteLine("Dumping JPEG file: {0}", args[0]);
                    Console.WriteLine("=============================================================================");
                    new JpegStreamReader(stream).Dump();
                }
            }
            catch (IOException e)
            {
                Console.WriteLine("Failed to open \\ parse file {0}, error: {1}", args[0], e.Message);
            }
        }
   }
}
