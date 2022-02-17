using System;
using System.Linq;
using System.IO;
using CSCore;
using PlaylistsNET.Models;
using CSCore.MediaFoundation;
using CSCore.Codecs;
using log4net;

namespace AudioStreamPoc
{
    public class WavToMp3Converter
    {
        private static readonly ILog Log = LogManager.GetLogger(nameof(WavToMp3Converter));

        public bool CreateMp3FromWav(M3uPlaylistEntry entry, string wavFilePath)
        {
            // eliminate invalid characters from the track title to ensure the file saves
            string sanitizedEntryTitle = Program.StripInvalidFilenameCharacters(entry.Title);

            var mp3Filename = $"{sanitizedEntryTitle}.mp3";
            var successful = ConvertWavToMp3(wavFilePath, mp3Filename);

            if (successful)
            {
                var fileMetadata = TagLib.File.Create(mp3Filename);
                fileMetadata.Tag.Album = entry.Album;
                fileMetadata.Tag.Performers = new string[] { entry.AlbumArtist };
                fileMetadata.Tag.Title = entry.Title;
                fileMetadata.Save();

                File.Delete(wavFilePath);
            }

            return successful;
        }

        private bool ConvertWavToMp3(string wavFilePath, string mp3FileName)
        {
            if (!File.Exists(wavFilePath))
            {
                Log.Error($"Unable to find wav file {wavFilePath}");
                return false;
            }

            var supportedFormats = MediaFoundationEncoder.GetEncoderMediaTypes(AudioSubTypes.MpegLayer3);
            if (!supportedFormats.Any())
            {
                Log.Error("The current platform does not support mp3 encoding.");
                return false;
            }

            IWaveSource source;
            try
            {
                source = CodecFactory.Instance.GetCodec(wavFilePath);

                if (
                    supportedFormats.All(
                        x => x.SampleRate != source.WaveFormat.SampleRate && x.Channels == source.WaveFormat.Channels))
                {
                    //the encoder does not support the input sample rate -> convert it to any supported samplerate
                    //choose the best sample rate with stereo (in order to make simple, we always use stereo in this sample)
                    int sampleRate =
                        supportedFormats.OrderBy(x => Math.Abs(source.WaveFormat.SampleRate - x.SampleRate))
                            .First(x => x.Channels == source.WaveFormat.Channels)
                            .SampleRate;

                    Log.Info($"Samplerate {source.WaveFormat.SampleRate} -> {sampleRate}");
                    Log.Info($"Channels {source.WaveFormat.Channels} -> {2}");
                    source = source.ChangeSampleRate(sampleRate);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Format not supported.", ex);
                return false;
            }

            using (source)
            {
                using (var encoder = MediaFoundationEncoder.CreateMP3Encoder(source.WaveFormat, mp3FileName))
                {
                    Log.Info($"\nWriting {mp3FileName}. . .");
                    byte[] buffer = new byte[source.WaveFormat.BytesPerSecond];
                    int read;
                    double logMessageThreshold = .10;
                    double logMessageIncrement = .10;
                    while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        encoder.Write(buffer, 0, read);

                        Console.CursorLeft = 0;
                        var writePercentage = (double)source.Position / source.Length;
                        if (writePercentage >= logMessageThreshold)
                        {
                            string writePercentageLogMessage = string.Format("{0:P}/{1:P}", writePercentage, 1);
                            Log.Info(writePercentageLogMessage);
                            logMessageThreshold += logMessageIncrement;
                        }
                    }
                }
            }
            return true;
        }
    }
}
