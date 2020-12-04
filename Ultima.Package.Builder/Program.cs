using System;
using System.IO;
using System.Linq;

namespace Ultima.Package.Builder
{
    internal class Program
    {
        public static int Main(string[] args)
        {
            return args.FirstOrDefault() switch
            {
                "export" when args.Length == 3 => Export(args[1], args[2]),
                "import" when args.Length == 3 => Import(args[1], args[2]),
                "--help" => Help(),
                _ => Help()
            };
        }
        
        public static int Export(string packagePath, string exportPath)
        {
            if (!File.Exists(packagePath))
            {
                Console.WriteLine("Invalid package path.");
                
                return -1;
            }

            if (!Directory.Exists(exportPath))
            {
                Console.WriteLine("Invalid export path.");
                
                return -2;
            }

            using var stream = File.OpenRead(packagePath);

            using var reader = new BinaryReader(stream);

            try
            {
                var package = UltimaPackage.FromReader(reader);

                package.Export(reader, exportPath);
            }
            catch (Exception e)
            {
                Console.WriteLine("Unable to export.");
                
                Console.WriteLine(e);

                return -3;
            }

            return 0;
        }

        public static int Import(string packagePath, string importPath)
        {
            if (!File.Exists(packagePath))
            {
                Console.WriteLine("Invalid package path.");

                return -1;
            }

            if (!Directory.Exists(importPath))
            {
                Console.WriteLine("Invalid import path.");

                return -2;
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
                var package = UltimaPackage.FromReader(reader);

                package.Import(reader, importPath);
                
                package.ToWriter(reader, writer);
            }
            catch (Exception e)
            {
                Console.WriteLine("Unable to import.");

                Console.WriteLine(e);

                return -3;
            }

            return 0;
        }

        public static int Help()
        {
            Console.WriteLine($"Ultima Package Builder");

            Console.WriteLine($"(c) 2020 CoreUO GPL\n");

            Console.WriteLine("Use export [package] [folder] or import [package] [folder].");

            return 0;
        }
    }
}
