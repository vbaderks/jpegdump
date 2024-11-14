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
                WriteLine(
                    $"{GetStartOffset():D8} Marker 0xFF{markerCode:X}. RST{markerCode - JpegMarker.Restart0} (Restart Marker {markerCode - JpegMarker.Restart0}), defined in ITU T.81/IEC 10918-1");
                break;

            case JpegMarker.StartOfImage:
                DumpStartOfImageMarker();
                break;

            case JpegMarker.EndOfImage:
                WriteLine($"{GetStartOffset():D8} Marker 0xFFD9. EOI (End Of Image), defined in ITU T.81/IEC 10918-1");
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
                WriteLine($"{GetStartOffset():D8} Marker 0xFFE0. App0 (Application Data 0), defined in ITU T.81/IEC 10918-1");
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
                WriteLine($"{GetStartOffset():D8} Marker 0xFFFE. COM (Comment), defined in ITU T.81/IEC 10918-1");
                break;

            default:
                WriteLine($"{GetStartOffset():D8} Marker 0xFF{markerCode:X}");
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
        WriteLine($"{GetStartOffset():D8} Marker 0xFFD8: SOI (Start Of Image), defined in ITU T.81/IEC 10918-1");
    }

    private void DumpStartOfFrameJpegLS()
    {
        WriteLine($"{GetStartOffset():D8} Marker 0xFFF7: SOF_55 (Start Of Frame JPEG-LS), defined in ITU T.87/IEC 14495-1 JPEG LS");
        WriteLine($"{Position:D8}  Size = {ReadUInt16BigEndian()}");
        WriteLine($"{Position:D8}  Sample precision (P) = {_reader.ReadByte()}");
        WriteLine($"{Position:D8}  Number of lines (Y) = {ReadUInt16BigEndian()}");
        WriteLine($"{Position:D8}  Number of samples per line (X) = {ReadUInt16BigEndian()}");
        long position = Position;
        byte componentCount = _reader.ReadByte();
        WriteLine($"{position:D8}  Number of image components in a frame (Nf) = {componentCount}");
        for (int i = 0; i < componentCount; i++)
        {
            WriteLine($"{Position:D8}   Component identifier (Ci) = {_reader.ReadByte()}");

            position = Position;
            byte samplingFactor = _reader.ReadByte();
            WriteLine($"{position:D8}   H and V sampling factor (Hi + Vi) = {samplingFactor} ({samplingFactor >> 4} + {samplingFactor & 0xF})");
            WriteLine($"{Position:D8}   Quantization table (Tqi) [reserved, should be 0] = {_reader.ReadByte()}");
        }
    }

    private void DumpJpegLSExtendedParameters()
    {
        WriteLine($"{GetStartOffset():D8} Marker 0xFFF8: LSE (JPEG-LS ), defined in ITU T.87/IEC 14495-1 JPEG LS");
        WriteLine($"{Position:D8}  Size = {ReadUInt16BigEndian()}");
        byte type = _reader.ReadByte();

        Write($"{Position:D8}  Type = {type}");
        switch (type)
        {
            case 1:
                WriteLine(" (Preset coding parameters)");
                WriteLine($"{Position:D8}  MaximumSampleValue = {ReadUInt16BigEndian()}");
                WriteLine($"{Position:D8}  Threshold 1 = {ReadUInt16BigEndian()}");
                WriteLine($"{Position:D8}  Threshold 2 = {ReadUInt16BigEndian()}");
                WriteLine($"{Position:D8}  Threshold 3 = {ReadUInt16BigEndian()}");
                WriteLine($"{Position:D8}  Reset value = {ReadUInt16BigEndian()}");
                break;

            default:
                WriteLine(" (Unknown");
                break;
        }
    }

    private void DumpStartOfScan()
    {
        WriteLine($"{GetStartOffset():D8} Marker 0xFFDA: SOS (Start Of Scan), defined in ITU T.81/IEC 10918-1");
        WriteLine($"{Position:D8}  Size = {ReadUInt16BigEndian()}");
        byte componentCount = _reader.ReadByte();
        WriteLine($"{Position:D8}  Component Count = {componentCount}");
        for (int i = 0; i < componentCount; i++)
        {
            WriteLine($"{Position:D8}   Component identifier (Ci) = {_reader.ReadByte()}");
            byte mappingTableSelector = _reader.ReadByte();
            WriteLine($"{Position:D8}   Mapping table selector = {mappingTableSelector} {(mappingTableSelector == 0 ? "(None)" : string.Empty)}");
        }

        WriteLine($"{Position:D8}  Near lossless (NEAR parameter) = {_reader.ReadByte()}");
        byte interleaveMode = _reader.ReadByte();
        WriteLine($"{Position:D8}  Interleave mode (ILV parameter) = {interleaveMode} ({GetInterleaveModeName(interleaveMode)})");
        WriteLine($"{Position:D8}  Point Transform = {_reader.ReadByte()}");
    }

    private void DumpDefineRestartInterval()
    {
        WriteLine($"{GetStartOffset():D8} Marker 0xFFDD: DRI (Define Restart Interval), defined in ITU T.81/IEC 10918-1");
        ushort size = ReadUInt16BigEndian();
        WriteLine($"{Position:D8}  Size = {size}");

        // ISO/IEC 14495-1, C.2.5 extends DRI to allow usage of 2-4 bytes for the interval.
        switch (size)
        {
            case 4:
                WriteLine($"{Position:D8}  Restart Interval = {ReadUInt16BigEndian()}");
                break;

            case 5:
                WriteLine($"{Position:D8}  Restart Interval = {ReadUInt24BigEndian()}");
                break;

            case 6:
                WriteLine($"{Position:D8}  Restart Interval = {ReadUInt32BigEndian()}");
                break;

            default:
                break;
        }
    }

    private void DumpApplicationData7()
    {
        WriteLine($"{GetStartOffset():D8} Marker 0xFFE7: APP7 (Application Data 7), defined in ITU T.81/IEC 10918-1");
        int size = ReadUInt16BigEndian();
        WriteLine($"{Position:D8}  Size = {size}");
        byte[] dataBytes = _reader.ReadBytes(size - 2);

        TryDumpAsHPColorSpace(dataBytes);
    }

    private void DumpApplicationData8()
    {
        WriteLine($"{GetStartOffset():D8} Marker 0xFFE8: APP8 (Application Data 8), defined in ITU T.81/IEC 10918-1");
        int size = ReadUInt16BigEndian();
        WriteLine($"{Position:D8}  Size = {size}");
        byte[] dataBytes = _reader.ReadBytes(size - 2);

        if (TryDumpAsSpiffHeader(dataBytes))
            return;

        if (TryDumpAsSpiffEndOfDirectory(dataBytes))
            return;

        TryDumpAsHPColorTransformation(dataBytes);
    }

    private void DumpApplicationData14()
    {
        WriteLine($"{GetStartOffset():D8} Marker 0xFFEE: APP14 (Application Data 14), defined in ITU T.81/IEC 10918-1");
        int size = ReadUInt16BigEndian();
        WriteLine($"{Position - 2:D8}  Size = {size}");
        byte[] dataBytes = _reader.ReadBytes(size - 2);

        TryDumpAsAdobeApp14(dataBytes, Position - dataBytes.Length);
    }

    private bool TryDumpAsSpiffHeader(byte[] dataBuffer)
    {
        if (dataBuffer.Length < 30)
            return false;

        if (!(dataBuffer[0] == 'S' && dataBuffer[1] == 'P' && dataBuffer[2] == 'I' && dataBuffer[3] == 'F' && dataBuffer[4] == 'F'))
            return false;

        WriteLine($"{GetStartOffset() - 28:D8}  SPIFF Header, defined in ISO/IEC 10918-3, Annex F");
        WriteLine($"{GetStartOffset() - 26:D8}  High version = {dataBuffer[6]}");
        WriteLine($"{GetStartOffset() - 25:D8}  Low version = {dataBuffer[7]}");
        WriteLine($"{GetStartOffset() - 24:D8}  Profile id = {dataBuffer[8]}");
        WriteLine($"{GetStartOffset() - 23:D8}  Component count = {dataBuffer[9]}");
        WriteLine($"{GetStartOffset() - 22:D8}  Height = {ConvertToUint32BigEndian(dataBuffer, 10)}");
        WriteLine($"{GetStartOffset() - 18:D8}  Width = {ConvertToUint32BigEndian(dataBuffer, 14)}");
        WriteLine($"{GetStartOffset() - 14:D8}  Color Space = {dataBuffer[18]} ({GetColorSpaceName(dataBuffer[18])})");
        WriteLine($"{GetStartOffset() - 13:D8}  Bits per sample = {dataBuffer[19]}");
        WriteLine($"{GetStartOffset() - 12:D8}  Compression Type = {dataBuffer[20]} ({GetCompressionTypeName(dataBuffer[20])})");
        WriteLine($"{GetStartOffset() - 11:D8}  Resolution Units = {dataBuffer[21]} ({GetResolutionUnitsName(dataBuffer[21])})");
        WriteLine($"{GetStartOffset() - 10:D8}  Vertical resolution = {ConvertToUint32BigEndian(dataBuffer, 22)}");
        WriteLine($"{GetStartOffset() - 6:D8}  Horizontal resolution = {ConvertToUint32BigEndian(dataBuffer, 26)}");

        return true;
    }

    private bool TryDumpAsSpiffEndOfDirectory(byte[] dataBuffer)
    {
        if (dataBuffer.Length != 6)
            return false;

        uint entryType = ConvertToUint32BigEndian(dataBuffer, 0);
        if (entryType == 1)
        {
            WriteLine($"{GetStartOffset() - 4:D8}  SPIFF EndOfDirectory Entry, defined in ISO/IEC 10918-3, Annex F");
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

        WriteLine($"{GetStartOffset() - 3:D8}  HP colorXForm, defined by HP JPEG-LS implementation");
        WriteLine($"{GetStartOffset():D8}  Transformation = {dataBuffer[4]} ({GetHPColorTransformationName(dataBuffer[4])})");
    }

    private static void TryDumpAsAdobeApp14(byte[] dataBuffer, long startPosition)
    {
        if (dataBuffer.Length != 5 + 2 + 2 + 2 + 1)
            return;

        // Check for 'Adobe'
        if (!(dataBuffer[0] == 'A' && dataBuffer[1] == 'd' && dataBuffer[2] == 'o' && dataBuffer[3] == 'b' && dataBuffer[4] == 'e'))
            return;

        WriteLine($"{startPosition:D8}  APP14 'Adobe' identifier");
        int index = 5;
        uint version = ConvertToUint16FromBigEndian(dataBuffer, index);
        WriteLine($"{startPosition + index:D8}   Version {version}");
        index += 6;
        WriteLine($"{startPosition + index:D8}   ColorSpace {dataBuffer[index]} (0 = Unknown (monochrome or RGB), 1 = YCbCr, 2 = YCCK)");
    }

    private void TryDumpAsHPColorSpace(byte[] dataBuffer)
    {
        if (dataBuffer.Length != 5)
            return;

        // Check for 'colr' stored in little endian
        if (!(dataBuffer[0] == 0x72 && dataBuffer[1] == 0x6C && dataBuffer[2] == 0x6F && dataBuffer[3] == 0x63))
            return;

        WriteLine($"{GetStartOffset() - 3:D8}  HP color space, defined by HP JPEG-LS implementation");
        WriteLine($"{GetStartOffset():D8}  Color Space = {dataBuffer[4]} ({GetHPColorSpaceName(dataBuffer[4])})");
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
