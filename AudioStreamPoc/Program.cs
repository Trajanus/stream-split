using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CSCore;
using CSCore.SoundIn;
using CSCore.Codecs.WAV;
using CSCore.Codecs.MP3;
using PlaylistsNET.Models;
using PlaylistsNET.Content;
using CSCore.MediaFoundation;
using CSCore.Codecs;
using System.Text.RegularExpressions;

namespace AudioStreamPoc
{
    class Program
    {
        public static long BytesWrittenThreshold = 100000;
        public static WaveWriter writer;
        public static WasapiCapture staticCapture;
        public static int dumpCounter = 0;
        public static long bytesWritten = 0;
        public static readonly string InvalidCharacters = "<>:\"/\\|?*";


        static void Main(string[] args)
        {
            using (WasapiCapture capture = new WasapiLoopbackCapture())
            {
                staticCapture = capture;
                capture.Initialize();
                writer = new WaveWriter($"dump{dumpCounter}.wav", capture.WaveFormat);
                capture.DataAvailable += DataAvailable;
                capture.Start();
                Console.ReadKey();
                capture.Stop();
                if (!writer.IsDisposed)
                {
                    writer.Dispose();
                }
            }

            var examplePlaylist = GeneratePlaylist();
            int tracksWrittenCounter = 0;
            foreach(var entry in examplePlaylist.PlaylistEntries)
            {
                // eliminate invalid characters from the track title to ensure the file saves
                Regex pattern = new Regex($"[{InvalidCharacters}]");
                string sanitizedEntryTitle = pattern.Replace(entry.Title, string.Empty);

                ConvertWavToMp3($"dump{tracksWrittenCounter}.wav", $"{sanitizedEntryTitle}.mp3");
                tracksWrittenCounter++;
            }
        }

        private static void DataAvailable(object s, DataAvailableEventArgs e)
        {
            short[] buffer = new short[e.ByteCount / 2];
            Buffer.BlockCopy(e.Data, 0, buffer, 0, e.ByteCount);

            if(buffer.All(level => level == 0))
            {
                Console.WriteLine("silence detected");
                if (!writer.IsDisposed)
                {
                    writer.Dispose();

                    if(bytesWritten >= BytesWrittenThreshold)
                    {
                        bytesWritten = 0;
                        dumpCounter++;
                    }
                    else
                    {
                        File.Delete($"dump{dumpCounter}.wav");
                    }
                }
            }
            else
            {
                if (writer.IsDisposed)
                {
                    writer = new WaveWriter($"dump{dumpCounter}.wav", staticCapture.WaveFormat);
                }
                writer.Write(e.Data, e.Offset, e.ByteCount);
                bytesWritten += e.ByteCount;
            }
        }


        private static M3uPlaylist GeneratePlaylist()
        {
            M3uPlaylist playlist = new M3uPlaylist();
            playlist.IsExtended = true;
            playlist.PlaylistEntries.Add(new M3uPlaylistEntry()
            {
                Album = "...and all the pieces matter, Five years of Music from The Wire (delux version)",
                AlbumArtist = "The Neville Brothers",
                Duration = TimeSpan.FromSeconds(94),
                Title = "Way Down in the Hole"
            });

            playlist.PlaylistEntries.Add(new M3uPlaylistEntry()
            {
                Album = "Eagle Birds / Low/Hi",
                AlbumArtist = "The Black Keys",
                Duration = TimeSpan.FromSeconds(161),
                Title = "Eagle Birds"
            });

            playlist.PlaylistEntries.Add(new M3uPlaylistEntry()
            {
                Album = "Lo/Hi",
                AlbumArtist = "The Black Keys",
                Duration = TimeSpan.FromSeconds(177),
                Title = "Lo/Hi"
            });

            playlist.PlaylistEntries.Add(new M3uPlaylistEntry()
            {
                Album = "Burn Bright",
                AlbumArtist = "The Heavy",
                Duration = TimeSpan.FromSeconds(187),
                Title = "Burn Bright"
            });

            playlist.PlaylistEntries.Add(new M3uPlaylistEntry()
            {
                Album = "123456 / Don't Ever Let Em",
                AlbumArtist = "Fitz and The Tantrums",
                Duration = TimeSpan.FromSeconds(163),
                Title = "Don't Ever Let Em"
            });

            playlist.PlaylistEntries.Add(new M3uPlaylistEntry()
            {
                Album = "Original Motion Picture Sound Track",
                AlbumArtist = "Pilotpriest",
                Duration = TimeSpan.FromSeconds(305),
                Title = "Bonus Track - Switchblade"
            });

            playlist.PlaylistEntries.Add(new M3uPlaylistEntry()
            {
                Album = "The Therapist",
                AlbumArtist = "Foreign Air",
                Duration = TimeSpan.FromSeconds(123),
                Title = "The Therapist"
            });

            playlist.PlaylistEntries.Add(new M3uPlaylistEntry()
            {
                Album = "Bad To Worse",
                AlbumArtist = "Ra Ra Riot",
                Duration = TimeSpan.FromSeconds(240),
                Title = "Bad To Worse"
            });

            playlist.PlaylistEntries.Add(new M3uPlaylistEntry()
            {
                Album = "It Doesn't Matter Why",
                AlbumArtist = "Silversun Pickups",
                Duration = TimeSpan.FromSeconds(248),
                Title = "It Doesn't Matter Why"
            });

            playlist.PlaylistEntries.Add(new M3uPlaylistEntry()
            {
                Album = "Cheeba Gold",
                AlbumArtist = "Emancipator, 9 Theory",
                Duration = TimeSpan.FromSeconds(252),
                Title = "Bombilla"
            });

            return playlist;
        }

        private static void ConvertWavToMp3(string wavFilePath, string mp3FileName)
        {
            if (!File.Exists(wavFilePath))
            {
                Console.WriteLine($"Unable to find wav file {wavFilePath}");
                return;
            }

            var supportedFormats = MediaFoundationEncoder.GetEncoderMediaTypes(AudioSubTypes.MpegLayer3);
            if (!supportedFormats.Any())
            {
                Console.WriteLine("The current platform does not support mp3 encoding.");
                return;
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

                    Console.WriteLine("Samplerate {0} -> {1}", source.WaveFormat.SampleRate, sampleRate);
                    Console.WriteLine("Channels {0} -> {1}", source.WaveFormat.Channels, 2);
                    source = source.ChangeSampleRate(sampleRate);
                }
            }
            catch (Exception)
            {
                Console.WriteLine("Format not supported.");
                return;
            }

            using (source)
            {
                using (var encoder = MediaFoundationEncoder.CreateMP3Encoder(source.WaveFormat, mp3FileName))
                {
                    byte[] buffer = new byte[source.WaveFormat.BytesPerSecond];
                    int read;
                    while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        encoder.Write(buffer, 0, read);

                        Console.CursorLeft = 0;
                        Console.Write("{0:P}/{1:P}", (double)source.Position / source.Length, 1);
                    }
                }
            }
        }
    }
}