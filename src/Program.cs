// Copyright (c) Victor Derks.
// SPDX-License-Identifier: MIT

using System;
using System.IO;
using static System.Console;

namespace JpegDump
{
    internal enum JpegMarker
    {
        StartOfImage = 0xD8,               // SOI
        EndOfImage = 0xD9,                 // EOI
        StartOfScan = 0xDA,                // SOS
        StartOfFrameJpegLS = 0xF7,         // SOF_55: Marks the start of a (JPEG-LS) encoded frame.
        JpegLSExtendedParameters = 0xF8,   // LSE: JPEG-LS extended parameters.
        ApplicationData0 = 0xE0,           // APP0: Application data 0: used for JFIF header.
        ApplicationData7 = 0xE7,           // APP7: Application data 7: color space.
        ApplicationData8 = 0xE8,           // APP8: Application data 8: colorXForm.
        Comment = 0xFE                     // COM:  Comment block.
    }

    internal sealed class JpegStreamReader : IDisposable
    {
        private readonly BinaryReader _reader;
        private bool _jpegLSStream;

        public JpegStreamReader(Stream stream)
        {
            _reader = new BinaryReader(stream);
        }

        public void Dispose()
        {
            _reader.Dispose();
        }

        public void Dump()
        {
            int c;
            while ((c = _reader.BaseStream.ReadByte()) != -1)
            {
                if (c != 0xFF) continue;

                int markerCode = _reader.BaseStream.ReadByte();
                if (IsMarkerCode(markerCode))
                {
                    DumpMarker(markerCode);
                }
            }
        }

        private bool IsMarkerCode(int code)
        {
            // To prevent marker codes in the encoded bit stream encoders must encode the next byte zero or the next bit zero (jpeg-ls).
            if (_jpegLSStream)
                return (code & 0x80) == 0X80;

            return code > 0;
        }

        private void DumpMarker(int markerCode)
        {
            //  FFD0 to FFD9 and FF01, markers without size.

            switch ((JpegMarker)markerCode)
            {
                case JpegMarker.StartOfImage:
                    DumpStartOfImageMarker();
                    break;

                case JpegMarker.EndOfImage:
                    WriteLine("{0:D8} Marker 0xFFD9. EOI (End Of Image), defined in ITU T.81/IEC 10918-1", GetStartOffset());
                    break;

                case JpegMarker.StartOfFrameJpegLS:
                    _jpegLSStream = true;
                    DumpStartOfFrameJpegLS();
                    break;

                case JpegMarker.JpegLSExtendedParameters:
                    DumpJpegLSExtendedParameters();
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

                case JpegMarker.ApplicationData0:
                    WriteLine("{0:D8} Marker 0xFFE0. App0 (Application Data 0), defined in ITU T.81/IEC 10918-1", GetStartOffset());
                    break;

                case JpegMarker.Comment:
                    WriteLine("{0:D8} Marker 0xFFFE. COM (Comment), defined in ITU T.81/IEC 10918-1", GetStartOffset());
                    break;

                default:
                    WriteLine("{0:D8} Marker 0xFF{1:X}", GetStartOffset(), markerCode);
                    break;
            }
        }

        private long GetStartOffset()
        {
            return _reader.BaseStream.Position - 2;
        }

        private long Position => _reader.BaseStream.Position;


        private void DumpStartOfImageMarker()
        {
            WriteLine("{0:D8} Marker 0xFFD8: SOI (Start Of Image), defined in ITU T.81/IEC 10918-1", GetStartOffset());
        }

        private void DumpStartOfFrameJpegLS()
        {
            WriteLine("{0:D8} Marker 0xFFF7: SOF_55 (Start Of Frame Jpeg-LS), defined in ITU T.87/IEC 14495-1 JPEG LS", GetStartOffset());
            WriteLine("{0:D8}  Size = {1}", Position, ReadUInt16BigEndian());
            WriteLine("{0:D8}  Sample precision (P) = {1}", Position, _reader.ReadByte());
            WriteLine("{0:D8}  Number of lines (Y) = {1}", Position, ReadUInt16BigEndian());
            WriteLine("{0:D8}  Number of samples per line (X) = {1}", Position, ReadUInt16BigEndian());
            byte componentCount = _reader.ReadByte();
            WriteLine("{0:D8}  Number of image components in a frame (Nf) = {1}", Position, componentCount);
            for (int i = 0; i < componentCount; i++)
            {
                WriteLine("{0:D8}   Component identifier (Ci) = {1}", Position, _reader.ReadByte());
                WriteLine("{0:D8}   H and V sampling factor (Hi + Vi) = {1}", Position, _reader.ReadByte());
                WriteLine("{0:D8}   Quantization table (Tqi) [reserved, should be 0] = {1}", Position, _reader.ReadByte());
            }
        }

        private void DumpJpegLSExtendedParameters()
        {
            WriteLine("{0:D8} Marker 0xFFF8: LSE (JPEG-LS ), defined in ITU T.87/IEC 14495-1 JPEG LS", GetStartOffset());
            WriteLine("{0:D8}  Size = {1}", Position, ReadUInt16BigEndian());
            byte type = _reader.ReadByte();

            Write("{0:D8}  Type = {1}", Position, type);
            switch (type)
            {
                case 1:
                    WriteLine(" (Preset coding parameters)");
                    WriteLine("{0:D8}  MaximumSampleValue = {1}", Position, ReadUInt16BigEndian());
                    WriteLine("{0:D8}  Threshold 1 = {1}", Position, ReadUInt16BigEndian());
                    WriteLine("{0:D8}  Threshold 2 = {1}", Position, ReadUInt16BigEndian());
                    WriteLine("{0:D8}  Threshold 3 = {1}", Position, ReadUInt16BigEndian());
                    WriteLine("{0:D8}  Reset value = {1}", Position, ReadUInt16BigEndian());
                    break;

                default:
                    WriteLine(" (Unknown");
                    break;
            }
        }

        private void DumpStartOfScan()
        {
            WriteLine("{0:D8} Marker 0xFFDA: SOS (Start Of Scan), defined in ITU T.81/IEC 10918-1", GetStartOffset());
            WriteLine("{0:D8}  Size = {1}", Position, ReadUInt16BigEndian());
            byte componentCount = _reader.ReadByte();
            WriteLine("{0:D8}  Component Count = {1}", Position, componentCount);
            for (int i = 0; i < componentCount; i++)
            {
                WriteLine("{0:D8}   Component identifier (Ci) = {1}", Position, _reader.ReadByte());
                byte mappingTableSelector = _reader.ReadByte();
                WriteLine("{0:D8}   Mapping table selector = {1} {2}", Position, mappingTableSelector, mappingTableSelector == 0 ? "(None)": string.Empty);
            }

            WriteLine("{0:D8}  Near lossless (NEAR parameter) = {1}", Position, _reader.ReadByte());
            byte interleaveMode = _reader.ReadByte();
            WriteLine("{0:D8}  Interleave mode (ILV parameter) = {1} ({2})", Position, interleaveMode, GetInterleaveModeName(interleaveMode));
            WriteLine("{0:D8}  Point Transform = {1}", Position, _reader.ReadByte());
        }

        private void DumpApplicationData7()
        {
            WriteLine("{0:D8} Marker 0xFFE7: APP7 (Application Data 7), defined in ITU T.81/IEC 10918-1", GetStartOffset());
            int size = ReadUInt16BigEndian();
            WriteLine("{0:D8}  Size = {1}", Position, size);
            for (int i = 0; i < size - 2; i++)
            {
                _reader.ReadByte();
            }
        }

        private void DumpApplicationData8()
        {
            WriteLine("{0:D8} Marker 0xFFE8: APP8 (Application Data 8), defined in ITU T.81/IEC 10918-1", GetStartOffset());
            int size = ReadUInt16BigEndian();
            WriteLine("{0:D8}  Size = {1}", Position, size);
            for (int i = 0; i < size - 2; i++)
            {
                _reader.ReadByte();
            }
        }

        private ushort ReadUInt16BigEndian()
        {
            return (ushort)((_reader.ReadByte() << 8) | _reader.ReadByte());
        }

        private static string GetInterleaveModeName(byte interleaveMode)
        {
            switch (interleaveMode)
            {
                case 0: return "None";
                case 1: return "Line";
                case 2: return "Sample interleaved";
                default: return "Invalid";
            }
        }
    }


    public static class Program
    {
        private static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                WriteLine("Usage: jpegdump <filename>");
                return;
            }

            try
            {
                WriteLine("Dumping JPEG file: {0}", args[0]);
                WriteLine("=============================================================================");

                using var stream = new FileStream(args[0], FileMode.Open);
                using var reader = new JpegStreamReader(stream);
                reader.Dump();
            }
            catch (IOException e)
            {
                WriteLine("Failed to open \\ parse file {0}, error: {1}", args[0], e.Message);
            }
        }
    }
}
