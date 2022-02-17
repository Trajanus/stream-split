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
        private static readonly int ERROR_FILE_NOT_FOUND = 2;

        private static ILog Log = LogManager.GetLogger(nameof(AudioStreamPoc));

        static void Main(string[] args)
        {
            try
            {
                string playlistFilepath;
                if (0 == args.Length)
                {
                    playlistFilepath = GetPlaylistPathInput();
                }
                else
                {
                    playlistFilepath = args[0];
                }

                Log.Info($"Using playlist at filepath: {playlistFilepath}");

                if (!File.Exists(playlistFilepath))
                {
                    Log.Error($"Unable to find a file at {playlistFilepath}");
                    Environment.Exit(ERROR_FILE_NOT_FOUND);
                }

                string serializedPlaylist = File.ReadAllText(playlistFilepath);
                M3uPlaylist playlist = JsonConvert.DeserializeObject<M3uPlaylist>(serializedPlaylist);

                PlaylistCapture playlistCapture = new PlaylistCapture(playlist);
                playlistCapture.StartCapture();
                Log.Info("Capture started, press any key to finish audio capture.\n");
                Console.ReadKey();
                playlistCapture.StopCapture();

            }catch(Exception ex)
            {
                Log.Error("Unhandled exception", ex);
            }

            Log.Info("Finished audio capture, press any key to close.");
            Console.ReadKey();
        }

        public static string StripInvalidFilenameCharacters(string filename)
        {
            return Path.GetInvalidFileNameChars().Aggregate(filename, (current, c) => current.Replace(c.ToString(), string.Empty));
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

        private static string GetPlaylistPathInput()
        {
            string playlistPath = null;
            Console.WriteLine("Enter the file path to the playlist and then press enter:");
            while(null == playlistPath)
            {
                playlistPath = Console.ReadLine();
                if (!File.Exists(playlistPath))
                {
                    Console.WriteLine($"Unable to find a file at '{playlistPath}' - enter a valid path and press enter:");
                    playlistPath = null;
                }
            }

            return playlistPath;
        }
    }
}