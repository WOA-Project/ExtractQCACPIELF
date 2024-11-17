using UEFIReader;

namespace ExtractQCACPIELF
{
    internal class Program
    {
        static void Main(string[] args)
        {
            if (args.Length >= 2 && File.Exists(args[0]))
            {
                ParseACPI(args[0], args[1], args.Length > 2);
            }
            else
            {
                Console.WriteLine("Usage: <Path to ACPI.elf image> <Output Directory> [Optional; Decompress LZMA Flag; Can be anything]");
            }
        }

        static void ParseACPI(string filePath, string destination, bool Compressed = false)
        {
            byte[] Buffer = File.ReadAllBytes(filePath);

            if (Compressed)
            {
                Buffer = LZMA.Decompress(Buffer, 0, (ulong)Buffer.LongLength);
            }

            using Stream stream = new MemoryStream(Buffer);
            using BinaryReader br = new(stream);

            stream.Seek(1, SeekOrigin.Begin);
            string elfHeaderMagic = new(br.ReadChars(3));
            if (elfHeaderMagic != "ELF")
            {
                throw new Exception("Invalid ELF");
            }

            uint acpiSegmentOffset = 0x1000; // Older is 3000, investigate how to get properly

            stream.Seek(acpiSegmentOffset, SeekOrigin.Begin);
            string acpiHeaderMagic = new(br.ReadChars(4));
            if (acpiHeaderMagic != "ACPI")
            {
                throw new Exception("Invalid ACPI");
            }

            Console.WriteLine("Reading table descriptor");

            uint tableCount = br.ReadUInt32();
            List<(uint offset, uint tableSize, uint descriptorSize)> tableDescriptor = [];
            for (uint tableIndex = 0; tableIndex < tableCount; tableIndex++)
            {
                uint offset = br.ReadUInt32();
                uint tableSize = br.ReadUInt32();
                uint descriptorSize = br.ReadUInt32();
                tableDescriptor.Add((offset, tableSize, descriptorSize));

                Console.WriteLine($"Descriptor[{tableIndex}]: Offset: 0x{offset:X8} TableSize: 0x{tableSize:X8} DescriptorSize: 0x{descriptorSize:X8}");
            }

            stream.Seek(acpiSegmentOffset, SeekOrigin.Begin);
            foreach ((uint offset, uint tableSize, uint descriptorSize) in tableDescriptor)
            {
                stream.Seek(acpiSegmentOffset + offset, SeekOrigin.Begin);
                byte[] acpiTable = br.ReadBytes((int)tableSize);
                stream.Seek(acpiSegmentOffset + offset, SeekOrigin.Begin);
                string acpiTableName = new(br.ReadChars(4));
                Console.WriteLine($"Saving {acpiTableName}");
                File.WriteAllBytes(Path.Combine(destination, $"{acpiTableName}.aml"), acpiTable);
            }

            Console.WriteLine("Done");
        }
    }
}
