using System;
using System.IO;
using System.Text;
using Ionic.Zlib;

namespace Ultima.Package
{
    public class UltimaFile
    {
        public static readonly byte[] Buffer = new byte[32768];
        
        public int BlockId { get; }

        public int FileId { get; }

        public long DataOffset { get; private set; }
        
        public int DataHeaderSize { get; }
        
        public int CompressedDataSize { get; private set; }
        
        public int DecompressedDataSize { get; private set; }
        
        public ulong FileNameHash { get; set; }
        
        public uint DataHeaderHash { get; }
        
        public bool DataCompressed { get; set; }
        
        public Action<BinaryWriter> Modify { get; set; }

        public UltimaFile(int blockId, int fileId, long dataOffset, int dataHeaderSize, int compressedDataSize, int decompressedDataSize, ulong fileNameHash, uint dataHeaderHash, bool dataCompressed)
        {
            BlockId = blockId;

            FileId = fileId;
            
            DataOffset = dataOffset;
            
            DataHeaderSize = dataHeaderSize;
            
            CompressedDataSize = compressedDataSize;
            
            DecompressedDataSize = decompressedDataSize;
            
            FileNameHash = fileNameHash;
            
            DataHeaderHash = dataHeaderHash;
            
            DataCompressed = dataCompressed;
        }

        public override string ToString()
        {
            return Dictionary.Collection.TryGetValue(FileNameHash, out var name) ? $"{BlockId}.{FileId} {name}" : $"{BlockId}.{FileId}";
        }

        public byte[] GetHeader(BinaryReader reader)
        {
            reader.BaseStream.Seek(DataOffset, SeekOrigin.Begin);

            return reader.ReadBytes(DataHeaderSize);
        }
        
        public byte[] GetData(BinaryReader reader, bool compressed = false)
        {
            using var memoryStream = new MemoryStream();

            GetData(memoryStream, reader, compressed);

            return memoryStream.ToArray();
        }

        public void GetData(BinaryReader reader, Action<BinaryReader, int> action, bool compressed = false)
        {
            reader.BaseStream.Seek(DataOffset + DataHeaderSize, SeekOrigin.Begin);

            if (!DataCompressed || compressed) action(reader, DecompressedDataSize);
            else
            {
                using var zlibStream = new ZlibStream(reader.BaseStream, CompressionMode.Decompress, true);
                
                using var zlibReader = new BinaryReader(zlibStream, Encoding.Default, true);

                action(zlibReader, DecompressedDataSize);
            }
        }

        public void GetData(Stream stream, BinaryReader reader, bool compressed = false)
        {
            reader.BaseStream.Seek(DataOffset + DataHeaderSize, SeekOrigin.Begin);
            
            if (!DataCompressed || compressed)
            {
                for (var i = CompressedDataSize; i > 0; i -= Buffer.Length)
                {
                    var size = i > Buffer.Length ? Buffer.Length : i;

                    reader.Read(Buffer, 0, size);

                    stream.Write(Buffer, 0, size);
                }
            }
            else
            {
                using var zlibStream = new ZlibStream(stream, CompressionMode.Decompress);

                for (var i = CompressedDataSize; i > 0; i -= Buffer.Length)
                {
                    var size = i > Buffer.Length ? Buffer.Length : i;

                    reader.Read(Buffer, 0, size);

                    zlibStream.Write(Buffer, 0, size);
                }
            }
        }

        public void SetData<T>(BinaryReader reader, BinaryWriter writer, long oldDataOffset, long newDataOffset, int blockId, int fileId, T state, Func<BinaryWriter, int, int, T, bool> modifier)
        {
            DataOffset = newDataOffset;

            reader.BaseStream.Seek(oldDataOffset, SeekOrigin.Begin);

            writer.BaseStream.Seek(newDataOffset, SeekOrigin.Begin);

            reader.Read(Buffer, 0, DataHeaderSize);

            writer.Write(Buffer, 0, DataHeaderSize);

            reader.BaseStream.Seek(oldDataOffset + DataHeaderSize, SeekOrigin.Begin);

            writer.BaseStream.Seek(newDataOffset + DataHeaderSize, SeekOrigin.Begin);

            if ((Modify != null || modifier != null) && !DataCompressed)
            {
                Modify?.Invoke(writer);

                if (modifier?.Invoke(writer, blockId, fileId, state) == false) Copy();

                else CompressedDataSize = (int)(writer.BaseStream.Position - newDataOffset - DataHeaderSize);
            }

            else if ((Modify != null || modifier != null) && DataCompressed)
            {
                if(WriteWithAction()) CompressedDataSize = (int)(writer.BaseStream.Position - newDataOffset - DataHeaderSize);

                else Copy();

                bool WriteWithAction()
                {
                    using var zlib = new ZlibStream(writer.BaseStream, CompressionMode.Compress, CompressionLevel.BestSpeed, true);

                    using var zlibWriter = new BinaryWriter(zlib);

                    Modify?.Invoke(zlibWriter);

                    if (modifier?.Invoke(zlibWriter, blockId, fileId, state) == false) return false;

                    DecompressedDataSize = (int) zlib.TotalIn;

                    return true;
                }
            }

            else Copy();

            void Copy()
            {
                writer.BaseStream.Seek(newDataOffset + DataHeaderSize, SeekOrigin.Begin);

                for (var i = CompressedDataSize; i > 0; i -= Buffer.Length)
                {
                    var size = i > Buffer.Length ? Buffer.Length : i;

                    reader.Read(Buffer, 0, size);

                    writer.Write(Buffer, 0, size);
                }
            }
        }
    }
}
