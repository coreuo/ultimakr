using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Ultima.Package
{
    public class Dictionary
    {
        private static readonly Dictionary<ulong, string> Buffer = new();

        public static readonly Dictionary<ulong, string> Collection = GetCollection();

        public static Dictionary<ulong, string> GetCollection()
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Ultima.Package.Dictionary.bin");

            if (stream == null) throw new InvalidOperationException("Unable to load Dictionary.");

            using var reader = new BinaryReader(stream);
            
            reader.ReadBytes(4);

            Buffer.Clear();

            reader.ReadByte();

            while (stream.Position < stream.Length)
            {
                var hash = reader.ReadUInt64();
                
                string name = null;

                if (reader.ReadByte() == 1) name = reader.ReadString();

                if (!Buffer.ContainsKey(hash)) Buffer.Add(hash, name);
            }

            return Buffer;
        }
    }
}
