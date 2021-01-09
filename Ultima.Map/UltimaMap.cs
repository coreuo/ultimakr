using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ultima.Package;

namespace Ultima.Map
{
    public class UltimaMap
    {
        public static void Import(BinaryReader uopReader, BinaryWriter uopWriter, string mapPath, string indexPath, string staticsPath, string radarPath, bool mulInMemory = false, Action<int> progress = null)
        {
            using var mapStream = mulInMemory ? new MemoryStream(File.ReadAllBytes(mapPath)) : (Stream)File.OpenRead(mapPath);

            using var mapReader = new BinaryReader(mapStream);

            using var indexStream = mulInMemory ? new MemoryStream(File.ReadAllBytes(indexPath)) : (Stream)File.OpenRead(indexPath);

            using var indexReader = new BinaryReader(indexStream);

            using var staticsStream = mulInMemory ? new MemoryStream(File.ReadAllBytes(staticsPath)) : (Stream)File.OpenRead(staticsPath);

            using var staticsReader = new BinaryReader(staticsStream);

            using var radarStream = mulInMemory ? new MemoryStream(File.ReadAllBytes(radarPath)) : (Stream)File.OpenRead(radarPath);

            using var radarReader = new BinaryReader(radarStream);

            var package = UltimaPackage.FromReader(uopReader);

            var (width, height, facet) = (0, 0, 0);

            var firstBlock = package.GetBlocks(uopReader).First();

            var firstTwoFiles = firstBlock.GetFiles(uopReader).Take(2).ToArray();

            firstTwoFiles[0].GetData(uopReader, (r, _) =>
            {
                height = r.ReadInt32();

                width = r.ReadInt32();
            });

            firstTwoFiles[1].GetData(uopReader, (r, _) => facet = r.ReadByte());

            package.ToWriter(uopReader, uopWriter, (width, height, facet, mapReader, indexReader, staticsReader, radarReader, progress), (w, b, f, s) =>
            {
                if (b == 0 && f == 0) return false;

                var delimiters = new List<Action<byte>>();

                var statics = new Dictionary<int, Dictionary<(int x, int y),List<(ushort value, sbyte altitude, ushort unknown, ushort hue)>>>();

                var mapBlockId = 100 * b + f - 1;

                s.progress?.Invoke(100 * (mapBlockId + 1) / (width * height / 4096));

                for (var k = 0; k < 4096; k++)
                {
                    var (x, y) = GetCoordinatesFromUopIndex(mapBlockId, k, s.height);

                    var (mulMapBlock, mulBlockIndex) = GetMulIndexFromCoordinates(x, y, s.height);

                    var mulOffset = mulMapBlock * 4 + 4 + mulMapBlock * 64 * 3 + mulBlockIndex * 3;

                    if (mulOffset >= s.mapReader.BaseStream.Length) return false;

                    s.mapReader.BaseStream.Seek(mulOffset, SeekOrigin.Begin);

                    WriteTile(s.mapReader.ReadUInt16(), s.mapReader.ReadSByte());

                    void WriteTile(ushort value, sbyte altitude)
                    {
                        ushort graphic = 0;

                        byte unknown = 0;

                        if (Dictionary.Values.TryGetValue(value, out var val))
                        {
                            graphic = val.Item1;

                            unknown = val.Item2;
                        }

                        if (k == 0)
                        {
                            w.Write((byte)s.facet);

                            w.Write((ushort)mapBlockId);
                        }
                        else
                        {
                            w.Write((byte)0);

                            w.Write((byte)0);
                        }

                        w.Write(altitude);

                        w.Write(graphic);

                        w.Write(unknown);

                        w.Write((byte)(value & 0xFF));

                        w.Write((byte)(value >> 8));
                    }

                    delimiters.Clear();

                    if (x % 64 == 0) delimiters.Add(c => WriteDelimiter(x - 1, y, 0, c));

                    if (x % 64 == 0 && y % 64 == 0) delimiters.Add(c => WriteDelimiter(x - 1, y - 1, 1, c));

                    if (y % 64 == 0) delimiters.Add(c => WriteDelimiter(x, y - 1, 2, c));

                    if ((x + 1) % 64 == 0) delimiters.Add(c => WriteDelimiter(x + 1, y, 3, c));

                    if ((x + 1) % 64 == 0 && (y + 1) % 64 == 0) delimiters.Add(c => WriteDelimiter(x + 1, y + 1, 4, c));

                    if ((y + 1) % 64 == 0) delimiters.Add(c => WriteDelimiter(x, y + 1, 5, c));

                    if (x % 64 == 0 && (y + 1) % 64 == 0) delimiters.Add(c => WriteDelimiter(x - 1, y + 1, 6, c));

                    if (y % 64 == 0 && (x + 1) % 64 == 0) delimiters.Add(c => WriteDelimiter(x + 1, y - 1, 7, c));

                    for (var l = 0; l < delimiters.Count; l++)
                    {
                        if(l == 0) delimiters[l].Invoke((byte)delimiters.Count);

                        else delimiters[l].Invoke(0);
                    }

                    void WriteDelimiter(int refX, int refY, byte direction, byte count)
                    {
                        if (refX < 0 || refX >= width || refY < 0 || refY >= height) return;

                        var (refMulMapBlock, refMulBlockIndex) = GetMulIndexFromCoordinates(refX, refY, s.height);

                        var refMulOffset = refMulMapBlock * 4 + 4 + refMulMapBlock * 64 * 3 + refMulBlockIndex * 3;

                        if (refMulOffset >= s.mapReader.BaseStream.Length) return;

                        s.mapReader.BaseStream.Seek(refMulOffset, SeekOrigin.Begin);

                        var value = s.mapReader.ReadUInt16();

                        var altitude = s.mapReader.ReadSByte();

                        ushort graphic = 0;

                        byte unknown = 0;

                        if (Dictionary.Values.TryGetValue(value, out var val))
                        {
                            graphic = val.Item1;

                            unknown = val.Item2;
                        }

                        w.Write(count);

                        w.Write(direction);

                        w.Write(altitude);

                        w.Write(graphic);

                        w.Write(unknown);
                    }

                    if (!statics.TryGetValue(mulMapBlock, out var blockStatics)) ReadBlockStatics();

                    void ReadBlockStatics()
                    {
                        statics[mulMapBlock] = blockStatics = new Dictionary<(int x, int y), List<(ushort value, sbyte altitude, ushort unknown, ushort hue)>>();

                        s.indexReader.BaseStream.Seek(mulMapBlock * 12, SeekOrigin.Begin);

                        var indexValue = s.indexReader.ReadInt32();

                        var staticsEnd = indexValue + s.indexReader.ReadInt32();

                        s.indexReader.ReadInt32();

                        if (indexValue == -1) return;

                        s.staticsReader.BaseStream.Seek(indexValue, SeekOrigin.Begin);

                        while (true)
                        {
                            if (s.staticsReader.BaseStream.Position == staticsEnd) break;

                            var staticsValue = s.staticsReader.ReadUInt16();

                            s.radarReader.BaseStream.Seek(0x4000 + staticsValue, SeekOrigin.Begin);

                            var (staticX, staticY) = GetCoordinatesFromMulIndex(mulMapBlock, s.staticsReader.ReadByte() + 8 * s.staticsReader.ReadByte(), s.height);

                            var tileStatics = blockStatics.TryGetValue((staticX, staticY), out var entry) ? entry : blockStatics[(staticX, staticY)] = new List<(ushort value, sbyte altitude, ushort unknown, ushort hue)>();

                            tileStatics.Add((staticsValue, s.staticsReader.ReadSByte(), s.staticsReader.ReadUInt16(), s.radarReader.ReadUInt16()));
                        }
                    }

                    if (!blockStatics.TryGetValue((x, y), out var res)) continue;

                    foreach (var (@static, m) in res.Select((v, m) => (v, m)))
                    {
                        w.Write((byte)0);

                        w.Write(m == 0 ? (byte)res.Count : (byte)0);

                        w.Write(@static.value);

                        w.Write(@static.unknown);

                        w.Write(@static.altitude);

                        w.Write(@static.hue);
                    }
                }

                w.Write((byte)0);

                w.Write((byte)0);

                return true;
            });
        }

        public static (int x, int y) GetCoordinatesFromUopIndex(int block, int index, int height)
        {
            const int blockSize = 64;

            var blockHeight = height / blockSize;

            var x = block / blockHeight * blockSize + index / blockSize;

            var y = block % blockHeight * blockSize + index % blockSize;

            return (x, y);
        }

        public static (int x, int y) GetCoordinatesFromMulIndex(int block, int index, int height)
        {
            const int blockSize = 8;

            var blockHeight = height / blockSize;

            var x = block / blockHeight * blockSize + index % blockSize;

            var y = block % blockHeight * blockSize + index / blockSize;

            return (x, y);
        }

        public static (int block, int index) GetMulIndexFromCoordinates(int x, int y, int height)
        {
            const int blockSize = 8;

            var blockX = x / blockSize;

            var blockY = y / blockSize;

            var blockHeight = height / blockSize;

            var block = blockX * blockHeight + blockY;

            var inBlockX = x % blockSize;

            var inBlockY = y % blockSize;

            return (block, inBlockY * blockSize + inBlockX);
        }
    }
}
