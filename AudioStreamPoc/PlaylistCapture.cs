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
            if (e.Data.All(item => item == 0))
            {
                return;
            }
            double numBytesInTrack = (_playlist.PlaylistEntries[_playlistIndex].Duration.TotalMilliseconds / 1000) * e.Format.BytesPerSecond;
            int remainingBytesInTrack = (int)(numBytesInTrack - trackBytesWritten);
            int silenceIndex = -1;

            if (!started)
            {
                Log.Debug($"Started recording track: {_playlist.PlaylistEntries[_playlistIndex].Title}; expected bytes in track: {numBytesInTrack}.\n\n");
                started = true;
            }

            if(trackBytesWritten < BytesWrittenThreshold)
            {
                Log.Debug("Track Starting . . .");
                LogDataAvailableBuffer(e.Data);
            }

            if (remainingBytesInTrack < TrackEndCheckByteThreshold)
            {
                Log.Debug("Track Ending . . .");
                LogDataAvailableBuffer(e.Data);
                silenceIndex = FindSilenceIndex(e.Data);
            }

            trackBytesWritten += e.ByteCount;

            if (silenceIndex > 0 && trackBytesWritten >= BytesWrittenThreshold)
            {
                _playlistIndex++;
                trackBytesWritten = 0;
                started = false;
            }


        }

        //private void DataAvailable(object s, DataAvailableEventArgs e)
        //{
        //    double numBytesInTrack = (_playlist.PlaylistEntries[_playlistIndex].Duration.TotalMilliseconds / 1000) * e.Format.BytesPerSecond;
        //    int silenceIndex = -1;
        //    if (null == _writer || _writer.IsDisposed)
        //    {
        //        StartNewTrackRecording(numBytesInTrack);
        //        silenceIndex = FindSilenceIndex(e.Data);
        //    }

        //    int remainingBytesInTrack = (int)(numBytesInTrack - trackBytesWritten);

        //    if (remainingBytesInTrack < TrackEndCheckByteThreshold && silenceIndex == -1)
        //    {
        //        silenceIndex = FindSilenceIndex(e.Data);
        //        Log.Debug($"SilenceIndex: {silenceIndex}");
        //    }

        //    int bufferSize = silenceIndex > 0 ? silenceIndex : e.ByteCount;

        //    byte[] buffer = new byte[bufferSize];
        //    Buffer.BlockCopy(e.Data, 0, buffer, 0, bufferSize);
        //    _writer.Write(buffer, 0, bufferSize);
        //    trackBytesWritten += bufferSize;

        //    //bool allTrackDataWritten = bufferSize != e.ByteCount;

        //    if (silenceIndex > 0 && trackBytesWritten >= BytesWrittenThreshold)
        //    {
        //        _writer.Dispose();
        //        trackBytesWritten = 0;
        //        _playlistIndex++;
        //        if (_playlistIndex >= _playlist.PlaylistEntries.Count())
        //        {
        //            _capture.DataAvailable -= DataAvailable;
        //        }

        //        Log.Debug($"{nameof(e.ByteCount)}: {e.ByteCount} at time of track writing.");
        //        Log.Debug($"{nameof(bufferSize)}: {bufferSize} at time of track writing.");
        //        Log.Debug($"{nameof(silenceIndex)}: {silenceIndex} at time of track writing.");

        //        if (bufferSize < e.ByteCount)
        //        {
        //            Log.Debug($"Adding on {bufferSize} to track {_playlist.PlaylistEntries[_playlistIndex].Title} from prior buffer");
        //            numBytesInTrack = (long)(_playlist.PlaylistEntries[_playlistIndex].Duration.TotalMilliseconds / 1000) * e.Format.BytesPerSecond;
        //            StartNewTrackRecording(numBytesInTrack);

        //            int offSet = bufferSize;
        //            int newBufferSize = e.ByteCount - bufferSize;

        //            byte[] newBuffer = new byte[newBufferSize];
        //            Buffer.BlockCopy(e.Data, bufferSize, newBuffer, 0, newBufferSize);
        //            _writer.Write(newBuffer, 0, newBufferSize);
        //            trackBytesWritten += newBufferSize;
        //        }
        //    }
        //}

        public string GetFileName(M3uPlaylistEntry entry)
        {
            return $"{Program.StripInvalidFilenameCharacters(entry.Title)}.wav";
        }
    }
}
