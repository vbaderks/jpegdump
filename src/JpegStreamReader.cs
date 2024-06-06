// Copyright (c) Victor Derks.
// SPDX-License-Identifier: MIT

using static System.Console;

namespace JpegDump;

internal sealed class JpegStreamReader(Stream stream) : IDisposable
{
    private readonly BinaryReader _reader = new(stream);
    private bool _jpegLSStream;

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
        return _jpegLSStream ? (code & 0x80) == 0X80 : code > 0;
    }

    private void DumpMarker(int markerCode)
    {
        //  FFD0 to FFD9 and FF01, markers without size.

        switch ((JpegMarker)markerCode)
        {
            case JpegMarker.Restart0:
            case JpegMarker.Restart1:
            case JpegMarker.Restart2:
            case JpegMarker.Restart3:
            case JpegMarker.Restart4:
            case JpegMarker.Restart5:
            case JpegMarker.Restart6:
            case JpegMarker.Restart7:
                WriteLine("{0:D8} Marker 0xFF{1:X}. RST{2} (Restart Marker {2}), defined in ITU T.81/IEC 10918-1",
                    GetStartOffset(), markerCode, markerCode - JpegMarker.Restart0);
                break;

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

            case JpegMarker.DefineRestartInterval:
                DumpDefineRestartInterval();
                break;

            case JpegMarker.ApplicationData0:
                WriteLine("{0:D8} Marker 0xFFE0. App0 (Application Data 0), defined in ITU T.81/IEC 10918-1", GetStartOffset());
                break;

            case JpegMarker.ApplicationData7:
                DumpApplicationData7();
                break;

            case JpegMarker.ApplicationData8:
                DumpApplicationData8();
                break;

            case JpegMarker.ApplicationData14:
                DumpApplicationData14();
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
        WriteLine("{0:D8} Marker 0xFFF7: SOF_55 (Start Of Frame JPEG-LS), defined in ITU T.87/IEC 14495-1 JPEG LS", GetStartOffset());
        WriteLine("{0:D8}  Size = {1}", Position, ReadUInt16BigEndian());
        WriteLine("{0:D8}  Sample precision (P) = {1}", Position, _reader.ReadByte());
        WriteLine("{0:D8}  Number of lines (Y) = {1}", Position, ReadUInt16BigEndian());
        WriteLine("{0:D8}  Number of samples per line (X) = {1}", Position, ReadUInt16BigEndian());
        long position = Position;
        byte componentCount = _reader.ReadByte();
        WriteLine("{0:D8}  Number of image components in a frame (Nf) = {1}", position, componentCount);
        for (int i = 0; i < componentCount; i++)
        {
            WriteLine("{0:D8}   Component identifier (Ci) = {1}", Position, _reader.ReadByte());

            position = Position;
            byte samplingFactor = _reader.ReadByte();
            WriteLine("{0:D8}   H and V sampling factor (Hi + Vi) = {1} ({2} + {3})", position, samplingFactor, samplingFactor >> 4, samplingFactor & 0xF);
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
            WriteLine("{0:D8}   Mapping table selector = {1} {2}", Position, mappingTableSelector, mappingTableSelector == 0 ? "(None)" : string.Empty);
        }

        WriteLine("{0:D8}  Near lossless (NEAR parameter) = {1}", Position, _reader.ReadByte());
        byte interleaveMode = _reader.ReadByte();
        WriteLine("{0:D8}  Interleave mode (ILV parameter) = {1} ({2})", Position, interleaveMode, GetInterleaveModeName(interleaveMode));
        WriteLine("{0:D8}  Point Transform = {1}", Position, _reader.ReadByte());
    }

    private void DumpDefineRestartInterval()
    {
        WriteLine("{0:D8} Marker 0xFFDD: DRI (Define Restart Interval), defined in ITU T.81/IEC 10918-1", GetStartOffset());
        ushort size = ReadUInt16BigEndian();
        WriteLine("{0:D8}  Size = {1}", Position, size);

        // ISO/IEC 14495-1, C.2.5 extends DRI to allow usage of 2-4 bytes for the interval.
        switch (size)
        {
            case 4:
                WriteLine("{0:D8}  Restart Interval = {1}", Position, ReadUInt16BigEndian());
                break;

            case 5:
                WriteLine("{0:D8}  Restart Interval = {1}", Position, ReadUInt24BigEndian());
                break;

            case 6:
                WriteLine("{0:D8}  Restart Interval = {1}", Position, ReadUInt32BigEndian());
                break;

            default:
                break;
        }
    }

    private void DumpApplicationData7()
    {
        WriteLine("{0:D8} Marker 0xFFE7: APP7 (Application Data 7), defined in ITU T.81/IEC 10918-1", GetStartOffset());
        int size = ReadUInt16BigEndian();
        WriteLine("{0:D8}  Size = {1}", Position, size);
        byte[] dataBytes = _reader.ReadBytes(size - 2);

        TryDumpAsHPColorSpace(dataBytes);
    }

    private void DumpApplicationData8()
    {
        WriteLine("{0:D8} Marker 0xFFE8: APP8 (Application Data 8), defined in ITU T.81/IEC 10918-1", GetStartOffset());
        int size = ReadUInt16BigEndian();
        WriteLine("{0:D8}  Size = {1}", Position, size);
        byte[] dataBytes = _reader.ReadBytes(size - 2);

        if (TryDumpAsSpiffHeader(dataBytes))
            return;

        if (TryDumpAsSpiffEndOfDirectory(dataBytes))
            return;

        TryDumpAsHPColorTransformation(dataBytes);
    }

    private void DumpApplicationData14()
    {
        WriteLine("{0:D8} Marker 0xFFEE: APP14 (Application Data 14), defined in ITU T.81/IEC 10918-1",
            GetStartOffset());
        int size = ReadUInt16BigEndian();
        WriteLine("{0:D8}  Size = {1}", Position - 2, size);
        byte[] dataBytes = _reader.ReadBytes(size - 2);

        TryDumpAsAdobeApp14(dataBytes, Position - dataBytes.Length);
    }

    private bool TryDumpAsSpiffHeader(byte[] dataBuffer)
    {
        if (dataBuffer.Length < 30)
            return false;

        if (!(dataBuffer[0] == 'S' && dataBuffer[1] == 'P' && dataBuffer[2] == 'I' && dataBuffer[3] == 'F' && dataBuffer[4] == 'F'))
            return false;

        WriteLine("{0:D8}  SPIFF Header, defined in ISO/IEC 10918-3, Annex F", GetStartOffset() - 28);
        WriteLine("{0:D8}  High version = {1}", GetStartOffset() - 26, dataBuffer[6]);
        WriteLine("{0:D8}  Low version = {1}", GetStartOffset() - 25, dataBuffer[7]);
        WriteLine("{0:D8}  Profile id = {1}", GetStartOffset() - 24, dataBuffer[8]);
        WriteLine("{0:D8}  Component count = {1}", GetStartOffset() - 23, dataBuffer[9]);
        WriteLine("{0:D8}  Height = {1}", GetStartOffset() - 22, ConvertToUint32BigEndian(dataBuffer, 10));
        WriteLine("{0:D8}  Width = {1}", GetStartOffset() - 18, ConvertToUint32BigEndian(dataBuffer, 14));
        WriteLine("{0:D8}  Color Space = {1} ({2})", GetStartOffset() - 14, dataBuffer[18], GetColorSpaceName(dataBuffer[18]));
        WriteLine("{0:D8}  Bits per sample = {1}", GetStartOffset() - 13, dataBuffer[19]);
        WriteLine("{0:D8}  Compression Type = {1} ({2})", GetStartOffset() - 12, dataBuffer[20], GetCompressionTypeName(dataBuffer[20]));
        WriteLine("{0:D8}  Resolution Units = {1} ({2})", GetStartOffset() - 11, dataBuffer[21], GetResolutionUnitsName(dataBuffer[21]));
        WriteLine("{0:D8}  Vertical resolution = {1}", GetStartOffset() - 10, ConvertToUint32BigEndian(dataBuffer, 22));
        WriteLine("{0:D8}  Horizontal resolution = {1}", GetStartOffset() - 6, ConvertToUint32BigEndian(dataBuffer, 26));

        return true;
    }

    private bool TryDumpAsSpiffEndOfDirectory(byte[] dataBuffer)
    {
        if (dataBuffer.Length != 6)
            return false;

        uint entryType = ConvertToUint32BigEndian(dataBuffer, 0);
        if (entryType == 1)
        {
            WriteLine("{0:D8}  SPIFF EndOfDirectory Entry, defined in ISO/IEC 10918-3, Annex F",
                GetStartOffset() - 4);
        }

        return true;
    }

    private void TryDumpAsHPColorTransformation(byte[] dataBuffer)
    {
        if (dataBuffer.Length != 5)
            return;

        // Check for 'xfrm' stored in little endian
        if (!(dataBuffer[0] == 0x6D && dataBuffer[1] == 0x72 && dataBuffer[2] == 0x66 && dataBuffer[3] == 0x78))
            return;

        WriteLine("{0:D8}  HP colorXForm, defined by HP JPEG-LS implementation", GetStartOffset() - 3);
        WriteLine("{0:D8}  Transformation = {1} ({2})", GetStartOffset(), dataBuffer[4], GetHPColorTransformationName(dataBuffer[4]));
    }

    private static void TryDumpAsAdobeApp14(byte[] dataBuffer, long startPosition)
    {
        if (dataBuffer.Length != 5 + 2 + 2 + 2 + 1)
            return;

        // Check for 'Adobe'
        if (!(dataBuffer[0] == 'A' && dataBuffer[1] == 'd' && dataBuffer[2] == 'o' && dataBuffer[3] == 'b' && dataBuffer[4] == 'e'))
            return;

        WriteLine("{0:D8}  APP14 'Adobe' identifier", startPosition);
        int index = 5;
        uint version = ConvertToUint16FromBigEndian(dataBuffer, index);
        WriteLine("{0:D8}   Version {1}", startPosition + index, version);
        index += 6;
        WriteLine("{0:D8}   ColorSpace {1} (0 = Unknown (monochrome or RGB), 1 = YCbCr, 2 = YCCK)", startPosition + index, dataBuffer[index]);
    }

    private void TryDumpAsHPColorSpace(byte[] dataBuffer)
    {
        if (dataBuffer.Length != 5)
            return;

        // Check for 'colr' stored in little endian
        if (!(dataBuffer[0] == 0x72 && dataBuffer[1] == 0x6C && dataBuffer[2] == 0x6F && dataBuffer[3] == 0x63))
            return;

        WriteLine("{0:D8}  HP color space, defined by HP JPEG-LS implementation", GetStartOffset() - 3);
        WriteLine("{0:D8}  Color Space = {1} ({2})", GetStartOffset(), dataBuffer[4], GetHPColorSpaceName(dataBuffer[4]));
    }

    private ushort ReadUInt16BigEndian()
    {
        return (ushort)((_reader.ReadByte() << 8) | _reader.ReadByte());
    }

    private uint ReadUInt24BigEndian()
    {
        return (ushort)((_reader.ReadByte() << 16) | (_reader.ReadByte() << 8) | _reader.ReadByte());
    }

    private uint ReadUInt32BigEndian()
    {
        return (uint)((_reader.ReadByte() << 24) | (_reader.ReadByte() << 16) | (_reader.ReadByte() << 8) | _reader.ReadByte());
    }

    private static uint ConvertToUint32BigEndian(byte[] buffer, int index)
    {
        return (uint)((buffer[index] << 24) | (buffer[index + 1] << 16) | (buffer[index + 2] << 8) | buffer[index + 3]);
    }

    private static uint ConvertToUint16FromBigEndian(byte[] buffer, int index)
    {
        return (uint)((buffer[index] << 8) | buffer[index + 1]);
    }

    private static string GetInterleaveModeName(byte interleaveMode)
    {
        return interleaveMode switch {
            0 => "None",
            1 => "Line interleaved",
            2 => "Sample interleaved",
            _ => "Invalid"
        };
    }

    private static string GetColorSpaceName(byte colorSpace)
    {
        return colorSpace switch {
            0 => "Bi-level black",
            1 => "ITU-R BT.709 Video",
            2 => "None",
            3 => "ITU-R BT.601-1. (RGB)",
            4 => "ITU-R BT.601-1. (video)",
            8 => "Gray-scale",
            9 => "Photo CD™",
            10 => "RGB",
            11 => "CMY",
            12 => "CMYK",
            13 => "Transformed CMYK",
            14 => "CIE 1976(L * a * b *)",
            15 => "Bi-level white",
            _ => "Unknown"
        };
    }

    private static string GetCompressionTypeName(byte compressionType)
    {
        return compressionType switch {
            0 => "Uncompressed",
            1 => "Modified Huffman",
            2 => "Modified READ",
            3 => "Modified Modified READ",
            4 => "ISO/IEC 11544 (JBIG)",
            5 => "ISO/IEC 10918-1 or ISO/IEC 10918-3 (JPEG)",
            6 => "ISO/IEC 14495-1 or ISO/IEC 14495-2 (JPEG-LS)",
            _ => "Unknown"
        };
    }

    private static string GetResolutionUnitsName(byte resolutionUnit)
    {
        return resolutionUnit switch {
            0 => "Aspect Ratio",
            1 => "Dots per Inch",
            2 => "Dots per Centimeter",
            _ => "Unknown"
        };
    }

    private static string GetHPColorTransformationName(byte colorTransformation)
    {
        return colorTransformation switch {
            1 => "HP1",
            2 => "HP2",
            3 => "HP3",
            4 => "RGB as YUV lossy",
            5 => "Matrix",
            _ => "Unknown"
        };
    }

    private static string GetHPColorSpaceName(byte colorSpace)
    {
        return colorSpace switch {
            1 => "Gray",
            2 => "Palettized",
            3 => "RGB",
            4 => "YUV",
            5 => "HSV",
            6 => "HSB",
            7 => "HSL",
            8 => "LAB",
            9 => "CMYK",
            _ => "Unknown"
        };
    }
}
