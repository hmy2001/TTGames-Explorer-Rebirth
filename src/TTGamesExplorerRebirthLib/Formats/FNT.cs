using TTGamesExplorerRebirthLib.Formats.DDS;
using TTGamesExplorerRebirthLib.Helper;

namespace TTGamesExplorerRebirthLib.Formats
{
    public class FNTCharMapping
    {
        public float  X;
        public float  Y;
        public float  Width;
        public float  Height;
    }
    
    public class FNTUnicode
    {
        public char   UnicodeChar;
        public ushort FontMappingIndex;
    }

    /// <summary>
    ///     Give ft2 file data and deserialize it.
    /// </summary>
    /// <remarks>
    ///     Based on my own research (hmy2001).
    ///     Some fields found by aoto & Sycamore.
    /// </remarks>
    public class FNT
    {
        public FNTCharMapping[] CharMappingTables;
        public FNTUnicode[] UnicodeTables;
        public DDSImage  FontImage;

        public float MinHeight;
        public float BaseLine;
        public float SpaceWidth;
        public uint  SndId;
        public uint  IcGap;

        public FNT(string filePath)
        {
            Deserialize(File.ReadAllBytes(filePath));
        }

        public FNT(byte[] buffer)
        {
            Deserialize(buffer);
        }

        private void Deserialize(byte[] buffer)
        {
            using MemoryStream stream = new(buffer);
            using BinaryReader reader = new(stream);

            // Read header.

            uint fileSize              = reader.ReadUInt32();
            uint qfnHeaderOffset       = (uint) reader.BaseStream.Position + reader.ReadUInt32();
            uint fontImageOffset       = (uint) reader.BaseStream.Position + reader.ReadUInt32();
            
            stream.Position += 4;//Unknown
            
            reader.BaseStream.Seek(qfnHeaderOffset, SeekOrigin.Begin);
            
            stream.Position += 4;//Unknown
            
            uint fileVersion           = reader.ReadUInt16(); // Always 1 ?
            
            stream.Position += 2;//Unknown
            
            uint size                  = reader.ReadUInt32();
            
            uint charsCount            = reader.ReadUInt32();
            uint unicodeTableItemCount = reader.ReadUInt32(); // Seems to be aligned or something since the section have a lot of 0xFF

            MinHeight  = reader.ReadSingle();
            BaseLine   = reader.ReadSingle();
            SpaceWidth = reader.ReadSingle();
            
            stream.Position += 20;//Unknown
            
            uint charsMappingArrayOffset            = (uint) reader.BaseStream.Position + reader.ReadUInt32();
            uint unicodeTableOffset                 = (uint) reader.BaseStream.Position + reader.ReadUInt32();

            // Read chars mapping section.
            
            reader.BaseStream.Seek(charsMappingArrayOffset, SeekOrigin.Begin);

            CharMappingTables = new FNTCharMapping[charsCount];

            for (int i = 0; i < charsCount; i++)
            {
                CharMappingTables[i] = new()
                {
                    X      = reader.ReadSingle(),
                    Y      = reader.ReadSingle(),
                    Width  = reader.ReadSingle(),
                    Height = MinHeight
                };
            }

            // Read chars index section.
            
            reader.BaseStream.Seek(unicodeTableOffset, SeekOrigin.Begin);

            UnicodeTables = new FNTUnicode[unicodeTableItemCount];
            
            for (int i = 0; i < unicodeTableItemCount; i++)
            {
                UnicodeTables[i] = new()
                {
                    UnicodeChar      = Convert.ToChar(reader.ReadUInt16()),
                    FontMappingIndex = reader.ReadUInt16(),
                };
            }

            reader.BaseStream.Seek(fontImageOffset, SeekOrigin.Begin);
    
            FontImage = new DDSImage(reader.ReadBytes((int) (fileSize - fontImageOffset)));
        }
    }
}