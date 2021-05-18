using CSCore.Codecs.WAV;
using CSCore.SoundIn;
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
            string filename = GetFileName(_playlist.PlaylistEntries[_playlistIndex]);
            _writer = new WaveWriter(filename, _capture.WaveFormat);
            _capture.DataAvailable += DataAvailable;
            _recordingTimer.Start();
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
            short[] buffer = new short[e.ByteCount / 2];
            Buffer.BlockCopy(e.Data, 0, buffer, 0, e.ByteCount);

            if (buffer.All(level => level == 0) 
                && _recordingTimer.ElapsedMilliseconds >= _playlist.PlaylistEntries[_playlistIndex].Duration.TotalMilliseconds)
            {
                if (!_writer.IsDisposed)
                {
                    _writer.Dispose();

                    if (bytesWritten >= BytesWrittenThreshold)
                    {
                        bytesWritten = 0;
                        _playlistIndex++;
                        _recordingTimer.Restart();
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
                if (_writer.IsDisposed)
                {
                    string filename = GetFileName(_playlist.PlaylistEntries[_playlistIndex]);
                    _writer = new WaveWriter(filename, _capture.WaveFormat);
                }
                _writer.Write(e.Data, e.Offset, e.ByteCount);
                bytesWritten += e.ByteCount;
            }
        }

        public string GetFileName(M3uPlaylistEntry entry)
        {
            return $"{StripInvalidFilenameCharacters(entry.Title)}.wav";
        }

        private string StripInvalidFilenameCharacters(string filename)
        {
            return Path.GetInvalidFileNameChars().Aggregate(filename, (current, c) => current.Replace(c.ToString(), string.Empty));
        }
    }
}
