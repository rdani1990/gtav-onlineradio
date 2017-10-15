using NAudio.Flac;
using NAudio.Vorbis;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace GTAV_OnlineRadio.AsiLibrary.WaveStreams
{
    public static class WaveStreamHelper
    {
        public static bool IsWaveFormatEqual(WaveFormat wf1, WaveFormat wf2)
        {
            return (wf1.SampleRate == wf2.SampleRate &&
                    wf1.Encoding == wf2.Encoding &&
                    wf1.Channels == wf2.Channels);
        }

        public static WaveStream OpenAudioFile(string fileName)
        {
            if (String.IsNullOrEmpty(fileName))
            {
                return null;
            }

            string extension = Path.GetExtension(fileName).ToLower();
            switch (extension)
            {
                case ".mp3":
                    return new Mp3FileReader(fileName);
                case ".flac":
                    return new FlacReader(fileName);
                case ".ogg":
                    return new VorbisWaveReader(fileName);
                default:
                    throw new NotSupportedException($"File type '{extension}' not supported!");
            }
        }

        public static MetaData GetAudioMetaData(string fileName)
        {
            string extension = Path.GetExtension(fileName).ToLower();
            switch (extension)
            {
                    //using (var mp3 = new Mp3Stream(File.OpenRead(fileName)))
                    //{
                    //    var tag = mp3.GetTag(Id3TagFamily.FileStartTag);
                    //}
                case ".mp3":
                case ".flac":
                    var tag = TagLib.File.Create(fileName)?.Tag;
                    return new MetaData(tag?.Performers.FirstOrDefault(), tag?.Title);
                //var flacWaveStream = new FlacReader(fileName);
                //var fileStream = File.OpenRead(fileName);
                //int vorbisMetadataSectionIndex = flacWaveStream.Metadata.FindIndex(m => m.MetaDataType == FlacMetaDataType.VorbisComment);
                //var intBuffer = new byte[4]; // buffer to read a simple int value
                //var metaDataBuffer = new byte[1024];

                //fileStream.Seek(4, SeekOrigin.Current); // skip the "fLaC" header part

                //// skip metadata info until we reach the VorbisComment section
                //for (int i = 0; i < vorbisMetadataSectionIndex; i++)
                //{
                //    fileStream.Seek(4, SeekOrigin.Current); // skip metadata flags
                //    fileStream.Seek(flacWaveStream.Metadata[i].Length, SeekOrigin.Current); // skip the whole metadata
                //}
                //fileStream.Seek(4, SeekOrigin.Current); // skip metadata flags of VorbisComment section

                //int length = ReadInt(fileStream, intBuffer);
                //fileStream.Seek(length, SeekOrigin.Current); // skip libFLAC info

                //var comments = new string[ReadInt(fileStream, intBuffer)]; // read vorbis comment count, and initialize an array with it

                //for (int i = 0; i < comments.Length; i++)
                //{
                //    length = ReadInt(fileStream, intBuffer); // length of the upcoming comment entry

                //    if (metaDataBuffer.Length < length)
                //    {
                //        metaDataBuffer = new byte[length];
                //    }

                //    fileStream.Read(metaDataBuffer, 0, length); // read the current metadata entry
                //    using (var metaStreamReader = new StreamReader(new MemoryStream(metaDataBuffer, 0, length))) // and process it with a StreamReader
                //    {
                //        comments[i] = metaStreamReader.ReadToEnd();
                //    }
                //}

                //flacWaveStream.Close();
                //fileStream.Close();

                //return ProcessVorbisComments(comments);
                case ".ogg":
                    using (var vorbisWaveReader = new VorbisWaveReader(fileName))
                    {
                        return ProcessVorbisComments(vorbisWaveReader.Comments);
                    }
                default:
                    throw new NotSupportedException($"File type '{extension}' not supported!");
            }
        }

        private static int ReadInt(Stream stream, byte[] intBuffer)
        {
            stream.Read(intBuffer, 0, 4);
            return BitConverter.ToInt32(intBuffer, 0);
        }

        private static MetaData ProcessVorbisComments(string[] comments)
        {
            string artist = comments.FirstOrDefault(c => c.ToUpper().StartsWith("ARTIST="));
            string title = comments.FirstOrDefault(c => c.ToUpper().StartsWith("TITLE="));

            if (artist != null)
            {
                artist = Regex.Replace(artist, "^ARTIST=", "", RegexOptions.IgnoreCase);
            }
            if (title != null)
            {
                title = Regex.Replace(title, "^TITLE=", "", RegexOptions.IgnoreCase);
                title = Regex.Replace(title, " \\(Mid\\)$", "");
            }

            return new MetaData(artist, title);
        }
    }

    public class FlacMetadataz : FlacMetadata
    {
        public FlacMetadataz(FlacMetaDataType type, bool lastBlock, Int32 length) : base(type, lastBlock, length)
        {

        }
    }

    public class FlacMetadata2
    {
        public unsafe static FlacMetadata FromStream(Stream stream)
        {
            bool lastBlock = false;
            FlacMetaDataType type = FlacMetaDataType.Undef;
            int length = 0;

            byte[] b = new byte[4];
            if (stream.Read(b, 0, 4) <= 0)
                throw new FlacException(new EndOfStreamException("Could not read metadata"), FlacLayer.Metadata);

            fixed (byte* headerBytes = b)
            {
                FlacBitReader bitReader = new FlacBitReader(headerBytes, 0);

                lastBlock = bitReader.ReadBits(1) == 1;
                type = (FlacMetaDataType)bitReader.ReadBits(7);
                length = (int)bitReader.ReadBits(24);
                ////1000 0000
                //if (((b[0] & 0x80) >> 7) == 1)
                //    lastBlock = true;
                //type = (FlacMetaDataType)(b[0] & 0x7F);
                //int length = (b[1] + (b[2] << 8) + (b[3] << 16));
            }

            FlacMetadata data;
            long streamStartPosition = stream.Position;
            if ((int)type < 0 || (int)type > 6)
                return null;

            switch (type)
            {
                case FlacMetaDataType.StreamInfo:
                    data = new FlacMetadataStreamInfo(stream, length, lastBlock);
                    break;

                case FlacMetaDataType.Seektable:
                    data = new FlacMetadataSeekTable(stream, length, lastBlock);
                    break;

                default:
                    data = new FlacMetadataz(type, lastBlock, length); ;
                    break;
            }

            stream.Seek(length - (stream.Position - streamStartPosition), SeekOrigin.Current);
            return data;
        }

        public static List<FlacMetadata> ReadAllMetadataFromStream(Stream stream)
        {
            List<FlacMetadata> metaDataCollection = new List<FlacMetadata>();
            while (true)
            {
                FlacMetadata data = FromStream(stream);
                if (data != null)
                    metaDataCollection.Add(data);

                if (data == null || data.IsLastMetaBlock)
                    return metaDataCollection;
            }
        }

        protected FlacMetadata2(FlacMetaDataType type, bool lastBlock, Int32 length)
        {
            MetaDataType = type;
            IsLastMetaBlock = lastBlock;
            Length = length;
        }

        public FlacMetaDataType MetaDataType { get; private set; }

        public Boolean IsLastMetaBlock { get; private set; }

        public Int32 Length { get; private set; }
    }
}
