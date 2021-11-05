using CSCore.Codecs.WAV;
using CSCore.SoundIn;
using log4net;
using PlaylistsNET.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace AudioStreamPoc
{
    public class PlaylistCapture
    {
        private static ILog Log = LogManager.GetLogger(nameof(PlaylistCapture));

        public PlaylistCapture(M3uPlaylist playlist)
        {
            _playlist = playlist;
            _capture = new WasapiLoopbackCapture();
            _recordingTimer = new Stopwatch();
        }

        private WasapiCapture _capture;
        private WaveWriter _writer;
        private M3uPlaylist _playlist;
        private int _playlistIndex = 0;
        private Stopwatch _recordingTimer;

        private readonly long BytesWrittenThreshold = 100000;
        private long bytesWritten = 0;

        public void StartCapture()
        {
            _capture.Initialize();
            _capture.DataAvailable += DataAvailable;
            _capture.Start();
        }

        public void StopCapture()
        {
            _capture.Stop();
            if (!_writer.IsDisposed)
            {
                _writer.Dispose();
            }

            _capture.Dispose();
        }

        private void DataAvailable(object s, DataAvailableEventArgs e)
        {
            double totalTrackTicks = _playlist.PlaylistEntries[_playlistIndex].Duration.TotalMilliseconds * TimeSpan.TicksPerMillisecond;
            if (_recordingTimer.ElapsedTicks >= totalTrackTicks)
            {
                _writer.Write(e.Data, e.Offset, e.ByteCount);

                if (!_writer.IsDisposed)
                {
                    _writer.Dispose();

                    if (bytesWritten >= BytesWrittenThreshold)
                    {
                        bytesWritten = 0;
                        _playlistIndex++;
                        _recordingTimer.Restart();
                        if(_playlistIndex >= _playlist.PlaylistEntries.Count())
                        {
                            _capture.DataAvailable -= DataAvailable;
                        }
                    }
                    else
                    {
                        string filename = GetFileName(_playlist.PlaylistEntries[_playlistIndex]);
                        if (File.Exists(filename))
                        {
                            File.Delete(filename);
                        }
                    }
                }
            }
            else
            {
                if (null == _writer || _writer.IsDisposed)
                {
                    string filename = GetFileName(_playlist.PlaylistEntries[_playlistIndex]);
                    _writer = new WaveWriter(filename, _capture.WaveFormat);
                    _recordingTimer.Start();
                }
                _writer.Write(e.Data, e.Offset, e.ByteCount);
                bytesWritten += e.ByteCount;
            }
        }

        public string GetFileName(M3uPlaylistEntry entry)
        {
            return $"{Program.StripInvalidFilenameCharacters(entry.Title)}.wav";
        }
    }
}
