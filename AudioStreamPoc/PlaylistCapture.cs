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
        private readonly short SilentBytesThreshold = 300;
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

        private void StartNewTrackRecording()
        {
            string filename = GetFileName(_playlist.PlaylistEntries[_playlistIndex]);

            if (File.Exists(filename))
            {
                File.Delete(filename);
            }

            _writer = new WaveWriter(filename, _capture.WaveFormat);
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

        private void LogDataAvailableBuffer(byte[] audioData)
        {
            int noisyLeadingBytes = 0;
            int noisyTrailingBytes = 0;
            int contiguousSilentBytes = 0;
            int silenceAreaBytes = 0;

            int silenceStartIndex = 0;
            int silenceEndIndex = 0;

            int totalSilentBytes = 0;

            bool silentAreaHit = false;

            for (int i = 0; i < audioData.Length; i++)
            {
                if (audioData[i] == 0)
                {
                    if(contiguousSilentBytes == 0)
                    {
                        silenceStartIndex = i;
                    }
                    contiguousSilentBytes++;
                    totalSilentBytes++;
                }
                else
                {
                    if (contiguousSilentBytes > SilentBytesThreshold)
                    {
                        silenceAreaBytes = contiguousSilentBytes;
                        silentAreaHit = true;
                        silenceEndIndex = i;
                    }

                    contiguousSilentBytes = 0;
                    silenceStartIndex = 0;

                    if (silentAreaHit)
                    {
                        noisyTrailingBytes++;
                    }
                    else
                    {
                        noisyLeadingBytes++;
                    }
                }
            }

            Log.Debug($"{nameof(noisyLeadingBytes)}: {noisyLeadingBytes}.");
            Log.Debug($"{nameof(noisyTrailingBytes)}: {noisyTrailingBytes}.");
            Log.Debug($"{nameof(silenceAreaBytes)}: {silenceAreaBytes}.");
            Log.Debug($"{nameof(silenceStartIndex)}: {silenceStartIndex}.");
            Log.Debug($"{nameof(silenceEndIndex)}: {silenceEndIndex}.");
            Log.Debug($"{nameof(totalSilentBytes)}: {totalSilentBytes}.");
            Log.Debug($"{nameof(audioData.Length)}: {audioData.Length}.\n");
        }

        private bool started = false;

        private void DataAvailable(object s, DataAvailableEventArgs e)
        {
            double numBytesInTrack = (_playlist.PlaylistEntries[_playlistIndex].Duration.TotalMilliseconds / 1000) * e.Format.BytesPerSecond;
            int remainingBytesInTrack = (int)(numBytesInTrack - trackBytesWritten);

            bool bufferIsSilent = e.Data.All(item => item == 0);

            bool trackEndFound = started && remainingBytesInTrack < TrackEndCheckByteThreshold && bufferIsSilent;
            if (trackEndFound) 
            {
                _writer.Dispose();
                trackBytesWritten = 0;
                _playlistIndex++;
                if (_playlistIndex >= _playlist.PlaylistEntries.Count())
                {
                    _capture.DataAvailable -= DataAvailable;
                }
                started = false;
            }

            if (!started)
            {
                if (bufferIsSilent)
                {
                    return;
                }
                else
                {
                    Log.Debug($"Started recording track: {_playlist.PlaylistEntries[_playlistIndex].Title}; expected bytes in track: {numBytesInTrack}.\n\n");
                    StartNewTrackRecording();
                    started = true;
                }
            }

            byte[] buffer = new byte[e.ByteCount];
            Buffer.BlockCopy(e.Data, 0, buffer, 0, e.ByteCount);
            _writer.Write(buffer, 0, e.ByteCount);
            trackBytesWritten += e.ByteCount;
        }

        public string GetFileName(M3uPlaylistEntry entry)
        {
            return $"{Program.StripInvalidFilenameCharacters(entry.Title)}.wav";
        }
    }
}
