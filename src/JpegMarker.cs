// Copyright (c) Victor Derks.
// SPDX-License-Identifier: MIT

namespace JpegDump;

internal enum JpegMarker
{
    Restart0 = 0xD0,                   // RST0
    Restart1 = 0xD1,                   // RST1
    Restart2 = 0xD2,                   // RST2
    Restart3 = 0xD3,                   // RST3
    Restart4 = 0xD4,                   // RST4
    Restart5 = 0xD5,                   // RST5
    Restart6 = 0xD6,                   // RST6
    Restart7 = 0xD7,                   // RST7
    StartOfImage = 0xD8,               // SOI
    EndOfImage = 0xD9,                 // EOI
    StartOfScan = 0xDA,                // SOS
    DefineRestartInterval = 0xDD,      // DRI
    StartOfFrameJpegLS = 0xF7,         // SOF_55: Marks the start of a (JPEG-LS) encoded frame.
    JpegLSExtendedParameters = 0xF8,   // LSE: JPEG-LS extended parameters.
    ApplicationData0 = 0xE0,           // APP0: Application data 0: used for JFIF header.
    ApplicationData7 = 0xE7,           // APP7: Application data 7: color space.
    ApplicationData8 = 0xE8,           // APP8: Application data 8: colorXForm.
    ApplicationData14 = 0xEE,          // APP14: Application data 14: used by Adobe
    Comment = 0xFE                     // COM:  Comment block.
}