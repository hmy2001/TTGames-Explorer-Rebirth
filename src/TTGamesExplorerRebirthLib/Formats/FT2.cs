using TTGamesExplorerRebirthLib.Formats.DDS;
using TTGamesExplorerRebirthLib.Helper;

namespace TTGamesExplorerRebirthLib.Formats
{
    public class FT2CharMapping
    {
        public float  X;
        public float  Y;
        public float  Width;
        public float  Height;
    }
    
    public class FT2Unicode
    {
        public char   UnicodeChar;
        public ushort FontMappingIndex;
    }

    /// <summary>
    ///     Give ft2 file data and deserialize it.
    /// </summary>
    /// <remarks>
    ///     Based on my own research (Ac_K).
    ///     Some fields found by dniel888.
    /// </remarks>
    public class FT2
    {
        private const string MagicNfnt = "TNFN";
        private const string MagicVtor = "ROTV";

        public FT2CharMapping[] CharMappingTables;
        public FT2Unicode[] UnicodeTables;
        public DDSImage  FontImage;

        public float MinHeight;
        public float BaseLine;
        public float SpaceWidth;
        public uint  SndId;
        public uint  IcGap;

        public FT2(string filePath)
        {
            Deserialize(File.ReadAllBytes(filePath));
        }

        public FT2(byte[] buffer)
        {
            Deserialize(buffer);
        }

        private void Deserialize(byte[] buffer)
        {
            using MemoryStream stream = new(buffer);
            using BinaryReader reader = new(stream);

            // Read header.

            uint headerSize = reader.ReadUInt32BigEndian();
            uint fileVersion = reader.ReadUInt32BigEndian(); // Always 1 ?

            if (reader.ReadUInt32AsString() != MagicNfnt)
            {
                throw new InvalidDataException($"{stream.Position:x8}");
            }

            uint   headerVersion         = reader.ReadUInt32BigEndian();
            uint   unknown1              = reader.ReadUInt32();          // Always 0 ?
            ushort unknown2              = reader.ReadUInt16();          // Always 0 ?
            uint   unknownSize           = reader.ReadUInt32BigEndian(); // Size ? + 0X38 give the last ROTV section
            uint   charsCount            = reader.ReadUInt32BigEndian();
            uint   unicodeTableItemCount = reader.ReadUInt32BigEndian(); // Seems to be aligned or something since the section have a lot of 0xFF

            MinHeight  = reader.ReadSingleBigEndian();
            BaseLine   = reader.ReadSingleBigEndian();
            SpaceWidth = reader.ReadSingleBigEndian();
            SndId      = reader.ReadUInt32BigEndian();
            IcGap      = reader.ReadUInt32BigEndian();

            // Read chars mapping section.

            if (headerVersion > 2 && reader.ReadUInt32AsString() != MagicVtor)
            {
                throw new InvalidDataException($"{stream.Position:x8}");
            }

            if (reader.ReadUInt32BigEndian() != charsCount)
            {
                throw new InvalidDataException($"{stream.Position:x8}");
            }

            CharMappingTables = new FT2CharMapping[charsCount];

            for (int i = 0; i < charsCount; i++)
            {
                CharMappingTables[i] = new()
                {
                    X      = reader.ReadSingleBigEndian(),
                    Y      = reader.ReadSingleBigEndian(),
                    Width  = reader.ReadSingleBigEndian(),
                    Height = MinHeight
                };
            }

            // Read chars index section.

            if (headerVersion > 2 && reader.ReadUInt32AsString() != MagicVtor)
            {
                throw new InvalidDataException($"{stream.Position:x8}");
            }

            if (reader.ReadUInt32BigEndian() != unicodeTableItemCount)
            {
                throw new InvalidDataException($"{stream.Position:x8}");
            }

            UnicodeTables = new FT2Unicode[unicodeTableItemCount];
            
            for (int i = 0; i < unicodeTableItemCount; i++)
            {
                UnicodeTables[i] = new()
                {
                    UnicodeChar      = Convert.ToChar(reader.ReadUInt16BigEndian()),
                    FontMappingIndex = reader.ReadUInt16BigEndian(),
                };
            }

            // Read image section.

            if (headerVersion > 2 && reader.ReadUInt32AsString() != MagicVtor)
            {
                throw new InvalidDataException($"{stream.Position:x8}");
            }

            uint kerningSectionSize = reader.ReadUInt32BigEndian();
            for(int i = 0; i < kerningSectionSize; i++){
                char charA = Convert.ToChar(reader.ReadUInt16BigEndian());
                char charB = Convert.ToChar(reader.ReadUInt16BigEndian());
                float gap = reader.ReadSingleBigEndian();
            }

            FontImage = new DDSImage(reader.ReadBytes((int)(stream.Length - stream.Position)));
        }
    }
}