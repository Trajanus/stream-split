using System;
using System.IO;
using System.Linq;
using CSCore;
using PlaylistsNET.Models;
using CSCore.MediaFoundation;
using CSCore.Codecs;
using System.Text.RegularExpressions;
using log4net;
using Newtonsoft.Json;

namespace AudioStreamPoc
{
    class Program
    {
        // see https://docs.microsoft.com/en-us/windows/win32/debug/system-error-codes
        private static readonly int ERROR_BAD_ARGUMENTS = 160;
        private static readonly int ERROR_FILE_NOT_FOUND = 2;

        private static readonly string InvalidCharacters = "<>:\"/\\|?*";

        private static ILog Log = LogManager.GetLogger(nameof(AudioStreamPoc));

        static void Main(string[] args)
        {
            try
            {
                if (0 == args.Length)
                {
                    Log.Error("No playlist filepath was provided as input, this is required.");
                    Environment.Exit(ERROR_BAD_ARGUMENTS);
                }
                else
                {
                    Log.Info($"Using playlist at filepath: {args[0]}");
                }

                string playlistFilepath = args[0];
                if (!File.Exists(playlistFilepath))
                {
                    Log.Error($"Unable to find a file at {playlistFilepath}");
                    Environment.Exit(ERROR_FILE_NOT_FOUND);
                }

                string serializedPlaylist = File.ReadAllText(playlistFilepath);
                M3uPlaylist playlist = JsonConvert.DeserializeObject<M3uPlaylist>(serializedPlaylist);

                PlaylistCapture playlistCapture = new PlaylistCapture(playlist);
                playlistCapture.StartCapture();
                Log.Info("Capture started, press any key to finish audio capture and start conversion to mp3 files.\n");
                Console.ReadKey();
                playlistCapture.StopCapture();

                int tracksWrittenCounter = 0;
                foreach (var entry in playlist.PlaylistEntries)
                {
                    // eliminate invalid characters from the track title to ensure the file saves
                    Regex pattern = new Regex($"[{InvalidCharacters}]");
                    string sanitizedEntryTitle = pattern.Replace(entry.Title, string.Empty);

                    var mp3Filename = $"{sanitizedEntryTitle}.mp3";
                    var wavFilename = playlistCapture.GetFileName(entry);
                    var successful = ConvertWavToMp3(wavFilename, mp3Filename);
                    tracksWrittenCounter++;

                    if (successful)
                    {
                        var fileMetadata = TagLib.File.Create(mp3Filename);
                        fileMetadata.Tag.Album = entry.Album;
                        fileMetadata.Tag.Performers = new string[] { entry.AlbumArtist };
                        fileMetadata.Tag.Title = entry.Title;
                        fileMetadata.Save();

                        File.Delete(wavFilename);
                    }
                }
            }catch(Exception ex)
            {
                Log.Error("Unhandled exception", ex);
            }

            Log.Info("Finished mp3 conversion, press any key to close.");
            Console.ReadKey();
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

        private static bool ConvertWavToMp3(string wavFilePath, string mp3FileName)
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