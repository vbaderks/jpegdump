// Copyright (c) Victor Derks.
// SPDX-License-Identifier: MIT

using static System.Console;

if (args.Length < 1)
{
    WriteLine("Usage: jpegdump <filename>");
    return;
}

try
{
    WriteLine($"Dumping JPEG file: {args[0]}");
    WriteLine("=============================================================================");

    using var stream = new FileStream(args[0], FileMode.Open);
    using var reader = new JpegDump.JpegStreamReader(stream);
    reader.Dump();
}
catch (IOException e)
{
    WriteLine($@"Failed to open \ parse file {args[0]}, error: {e.Message}");
}
