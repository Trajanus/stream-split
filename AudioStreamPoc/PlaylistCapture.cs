using CSCore.Codecs.WAV;
using CSCore.SoundIn;
using log4net;
using PlaylistsNET.Models;
using System;
using System.IO;
using System.Linq;

namespace AudioStreamPoc
{
    public class PlaylistCapture
    {
        private static ILog Log = LogManager.GetLogger(nameof(PlaylistCapture));

        public PlaylistCapture(M3uPlaylist playlist)
        {
            _playlist = playlist;
            _capture = new WasapiLoopbackCapture();
        }

        private WasapiCapture _capture;
        private WaveWriter _writer;
        private M3uPlaylist _playlist;
        private int _playlistIndex = 0;

        private readonly long BytesWrittenThreshold = 100000;
        private readonly long TrackEndCheckByteThreshold = 100000;
        private readonly short SilentBytesThreshold = 30;
        private long trackBytesWritten = 0;

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

        private void StartNewTrackRecording(double numBytesInTrack)
        {
            string filename = GetFileName(_playlist.PlaylistEntries[_playlistIndex]);

            if (File.Exists(filename))
            {
                File.Delete(filename);
            }

            _writer = new WaveWriter(filename, _capture.WaveFormat);
            Log.Debug($"Started recording track: {_playlist.PlaylistEntries[_playlistIndex].Title}; expected bytes in track: {numBytesInTrack}.");
        }

        private int FindSilenceIndex(byte[] audioData)
        {
            int contiguousSilentBytes = 0;
            for(int i = 0; i < audioData.Length; i++)
            {
                if(audioData[i] == 0)
                {
                    contiguousSilentBytes++;
                }
                else
                {
                    contiguousSilentBytes = 0;
                }

                if(contiguousSilentBytes > SilentBytesThreshold)
                {
                    return i;
                }
            }

            return -1;
        }

        private void DataAvailable(object s, DataAvailableEventArgs e)
        {
            double numBytesInTrack = (_playlist.PlaylistEntries[_playlistIndex].Duration.TotalMilliseconds / 1000) * e.Format.BytesPerSecond;
            if (null == _writer || _writer.IsDisposed)
            {
                StartNewTrackRecording(numBytesInTrack);
            }

            int remainingBytesInTrack = (int)(numBytesInTrack - trackBytesWritten);

            int silenceIndex = -1;
            if(remainingBytesInTrack < TrackEndCheckByteThreshold)
            {
                silenceIndex = FindSilenceIndex(e.Data);
                Log.Debug($"SilenceIndex: {silenceIndex}");
            }

            int bufferSize = silenceIndex > 0 ? silenceIndex : e.ByteCount;

            byte[] buffer = new byte[bufferSize];
            Buffer.BlockCopy(e.Data, 0, buffer, 0, bufferSize);
            _writer.Write(buffer, 0, bufferSize);
            trackBytesWritten += bufferSize;

            //bool allTrackDataWritten = bufferSize != e.ByteCount;

            if (silenceIndex > 0 && trackBytesWritten >= BytesWrittenThreshold)
            {
                _writer.Dispose();
                trackBytesWritten = 0;
                _playlistIndex++;
                if (_playlistIndex >= _playlist.PlaylistEntries.Count())
                {
                    _capture.DataAvailable -= DataAvailable;
                }

                if (bufferSize < e.ByteCount)
                {
                    numBytesInTrack = (long)(_playlist.PlaylistEntries[_playlistIndex].Duration.TotalMilliseconds / 1000) * e.Format.BytesPerSecond;
                    StartNewTrackRecording(numBytesInTrack);

                    int offSet = bufferSize;
                    int newBufferSize = e.ByteCount - bufferSize;

                    byte[] newBuffer = new byte[newBufferSize];
                    Buffer.BlockCopy(e.Data, bufferSize, newBuffer, 0, newBufferSize);
                    _writer.Write(newBuffer, 0, newBufferSize);
                    trackBytesWritten += newBufferSize;
                }
            }
        }

        public string GetFileName(M3uPlaylistEntry entry)
        {
            return $"{Program.StripInvalidFilenameCharacters(entry.Title)}.wav";
        }
    }
}
