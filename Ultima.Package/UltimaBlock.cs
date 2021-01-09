using System;
using System.Collections.Generic;
using System.IO;

namespace Ultima.Package
{
    public class UltimaBlock
    {
        public const int FileHeaderSize = 34;

        private readonly Dictionary<long, UltimaFile> _files = new();
        
        public int BlockId { get; }

        public long FileOffset { get; private set; }

        public int FileCount { get; }

        public UltimaBlock(int blockId, long fileOffset, int fileCount)
        {
            BlockId = blockId;
            
            FileOffset = fileOffset;
            
            FileCount = fileCount;
        }

        private UltimaFile GetFile(BinaryReader reader, int fileId)
        {
            var offset = reader.ReadInt64();

            return _files.TryGetValue(offset, out var file) ? file : _files[offset] = new UltimaFile(BlockId, fileId, offset, reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadUInt64(), reader.ReadUInt32(), reader.ReadInt16() > 0);
        }

        public IEnumerable<UltimaFile> GetFiles(BinaryReader reader)
        {
            for (var i = 0; i < FileCount; i++)
            {
                reader.BaseStream.Seek(FileOffset + i * FileHeaderSize, SeekOrigin.Begin);
                
                yield return GetFile(reader, i);
            }
        }

        public void SetFiles<T>(BinaryReader reader, BinaryWriter writer, long oldFileOffset, long newFileOffset, int maxFiles, int blockId, T state, Func<BinaryWriter, int, int, T, bool> modifier)
        {
            FileOffset = newFileOffset;

            var oldDataOffset = oldFileOffset + maxFiles * FileHeaderSize;

            var newDataOffset = newFileOffset + maxFiles * FileHeaderSize;

            for (var i = 0; i < FileCount; i++)
            {
                reader.BaseStream.Seek(oldFileOffset + i * FileHeaderSize, SeekOrigin.Begin);

                var file = GetFile(reader, i);
                
                var oldDataSize = file.DataHeaderSize + file.CompressedDataSize;

                file.SetData(reader, writer, oldDataOffset, newDataOffset, blockId, i, state, modifier);

                oldDataOffset += oldDataSize;

                newDataOffset += file.DataHeaderSize + file.CompressedDataSize;

                writer.BaseStream.Seek(newFileOffset + i * FileHeaderSize, SeekOrigin.Begin);

                writer.Write(file.DataOffset);

                writer.Write(file.DataHeaderSize);

                writer.Write(file.CompressedDataSize);

                writer.Write(file.DecompressedDataSize);

                writer.Write(file.FileNameHash);

                writer.Write(file.DataHeaderHash);

                writer.Write(file.DataCompressed ? (short) 1 : (short) 0);
            }

            reader.BaseStream.Seek(oldDataOffset, SeekOrigin.Begin);

            writer.BaseStream.Seek(newDataOffset, SeekOrigin.Begin);
        }
    }
}
