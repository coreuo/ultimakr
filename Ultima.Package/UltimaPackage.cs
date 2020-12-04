using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ultima.Package
{
    public class UltimaPackage
    {
        private readonly Dictionary<long, UltimaBlock> _blocks = new();
        
        public const int BlockHeaderSize = 12;
        
        public int Version { get; }
        
        public uint Misc { get; }

        public long BlockOffset { get; }
        
        public int MaxFilesPerBlock { get; }
        
        public int FileCount { get; }
        
        public UltimaPackage(int version, uint misc, long blockOffset, int maxFilesPerBlock, int fileCount)
        {
            Version = version;
            
            Misc = misc;
            
            BlockOffset = blockOffset;
            
            MaxFilesPerBlock = maxFilesPerBlock;
            
            FileCount = fileCount;
        }

        public static UltimaPackage FromReader(BinaryReader reader)
        {
            reader.BaseStream.Seek(0, SeekOrigin.Begin);
            
            if (reader.ReadByte() != 'M' || reader.ReadByte() != 'Y' || reader.ReadByte() != 'P' || reader.ReadByte() != 0) throw new InvalidOperationException("Invalid stream.");

            return new UltimaPackage(reader.ReadInt32(), reader.ReadUInt32(), reader.ReadInt64(), reader.ReadInt32(), reader.ReadInt32());
        }

        public void ToWriter(BinaryReader reader, BinaryWriter writer)
        {
            writer.Write((byte) 'M');

            writer.Write((byte) 'Y');

            writer.Write((byte) 'P');

            writer.Write((byte) 0);

            writer.Write(Version);

            writer.Write(Misc);

            writer.Write(BlockOffset);

            writer.Write(MaxFilesPerBlock);

            writer.Write(FileCount);

            for (var i = 28; i < BlockOffset; i++) writer.Write((byte) 0);
            
            SetBlocks(reader, writer);
        }

        public void Export(BinaryReader reader, string path)
        {
            foreach (var (block, i) in GetBlocks(reader).Select((b,i) => (b,i)))
            {
                foreach (var (file, j) in block.GetFiles(reader).Select((f, j) => (f, j)))
                {
                    if (!Dictionary.Collection.TryGetValue(file.FileNameHash, out var name) || string.IsNullOrEmpty(name)) name = $"{i}.{j}.dat";

                    var fullName = Path.Combine(path, name);

                    var directory = Path.GetDirectoryName(fullName);

                    if (directory == null) throw new InvalidOperationException("Invalid directory.");
                    
                    Directory.CreateDirectory(directory);

                    using var stream = File.OpenWrite(fullName);

                    file.GetData(stream, reader);
                }
            }
        }

        public void Import(BinaryReader reader, string path)
        {
            foreach (var (block, i) in GetBlocks(reader).Select((b, i) => (b, i)))
            {
                foreach (var (file, j) in block.GetFiles(reader).Select((f, j) => (f, j)))
                {
                    if (!Dictionary.Collection.TryGetValue(file.FileNameHash, out var name) ||string.IsNullOrEmpty(name)) name = $"{i}.{j}.dat";

                    var fullName = Path.Combine(path, name);
                    
                    if(!File.Exists(fullName)) continue;

                    file.ModifyWithPath = fullName;
                }
            }
        }

        public IEnumerable<UltimaBlock> GetBlocks(BinaryReader reader)
        {
            reader.BaseStream.Seek(BlockOffset, SeekOrigin.Begin);
            
            long next;

            var id = 0;

            do
            {
                var count = reader.ReadInt32();
                
                next = reader.ReadInt64();

                yield return _blocks.TryGetValue(reader.BaseStream.Position, out var block) ? block : _blocks[reader.BaseStream.Position] = new UltimaBlock(id, reader.BaseStream.Position, count);

                reader.BaseStream.Seek(next, SeekOrigin.Begin);

                id++;
            }
            while (next != 0);
        }

        public void SetBlocks(BinaryReader reader, BinaryWriter writer)
        {
            var oldBlockOffset = BlockOffset;

            var newBlockOffset = BlockOffset;

            var id = 0;

            do
            {
                var oldFileOffset = oldBlockOffset + BlockHeaderSize;

                var newFileOffset = newBlockOffset + BlockHeaderSize;

                reader.BaseStream.Seek(oldBlockOffset, SeekOrigin.Begin);
                
                writer.BaseStream.Seek(newBlockOffset, SeekOrigin.Begin);

                var count = reader.ReadInt32();

                oldBlockOffset = reader.ReadInt64();

                writer.Write(count);

                var offset = writer.BaseStream.Position;

                var block = _blocks.TryGetValue(oldFileOffset, out var value) ? value : new UltimaBlock(id, oldFileOffset, count);

                block.SetFiles(reader, writer, oldFileOffset, newFileOffset, MaxFilesPerBlock);

                newBlockOffset = writer.BaseStream.Position;

                writer.BaseStream.Seek(offset, SeekOrigin.Begin);

                writer.Write(oldBlockOffset == 0 ? 0 : newBlockOffset);

                id++;
            }
            while (oldBlockOffset != 0);
        }
    }
}
