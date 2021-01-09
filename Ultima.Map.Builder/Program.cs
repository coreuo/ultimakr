using System;
using System.IO;
using System.Linq;

namespace Ultima.Map.Builder
{
    internal class Program
    {
        public static int Main(string[] args)
        {
            return args.FirstOrDefault() switch
            {
                "import" when args.Length is >= 6 and <= 7 => Import(args[1], args[2], args[3], args[4], args[5], args.Length > 6 && args[6] == "--fast"),
                "--help" => Help(),
                _ => Help()
            };
        }
        
        public static int Import(string packagePath, string mapPath, string indexPath, string staticsPath, string radarPath, bool fast = false)
        {
            if (!File.Exists(packagePath))
            {
                Console.WriteLine("Invalid package path.");

                return -1;
            }

            if (!File.Exists(mapPath))
            {
                Console.WriteLine("Invalid map path.");

                return -2;
            }

            if (!File.Exists(indexPath))
            {
                Console.WriteLine("Invalid index path.");

                return -3;
            }

            if (!File.Exists(staticsPath))
            {
                Console.WriteLine("Invalid statics path.");

                return -4;
            }

            if (!File.Exists(radarPath))
            {
                Console.WriteLine("Invalid radar path.");

                return -5;
            }

            var temp = Path.GetTempPath() + Guid.NewGuid() + ".uop";

            File.Copy(packagePath, temp);

            File.Delete(packagePath);

            using var inputStream = File.OpenRead(temp);

            using var reader = new BinaryReader(inputStream);

            using var outputStream = File.OpenWrite(packagePath);

            using var writer = new BinaryWriter(outputStream);

            try
            {
                UltimaMap.Import(reader, writer, mapPath, indexPath, staticsPath, radarPath, fast);
            }
            catch (Exception e)
            {
                Console.WriteLine("Unable to import.");

                Console.WriteLine(e);

                return -6;
            }

            Console.WriteLine("Import done.");

            return 0;
        }

        public static int Help()
        {
            Console.WriteLine($"UltimaKR Map Builder v1.1.0");

            Console.WriteLine($"(c) 2021 CoreUO GPL\n");

            Console.WriteLine("Use import [facet0.uop] [map0.mul] [staidx0.mul] [statics0.mul] [radarcol.mul] (--fast).");

            return 0;
        }
    }
}
