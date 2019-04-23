using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CSCore;
using CSCore.SoundIn;
using CSCore.Codecs.WAV;
using CSCore.Codecs.MP3;

namespace AudioStreamPoc
{
    class Program
    {
        public static WaveWriter writer;
        public static WasapiCapture staticCapture;
        public static int dumpCounter = 0;
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
        }

        private static void DataAvailable(object s, DataAvailableEventArgs e)
        {
            short[] buffer = new short[e.ByteCount / 2];
            Buffer.BlockCopy(e.Data, 0, buffer, 0, e.ByteCount);

            var silenceCount = buffer.Count(level => level == 0);
            if (silenceCount / buffer.Count() > .9)
            {
                Console.WriteLine($"silence threshold hit with value of: {silenceCount / buffer.Count()}");
                if (!writer.IsDisposed)
                {
                    writer.Dispose();
                    dumpCounter++;
                }
            }
            else
            {
                if (writer.IsDisposed)
                {
                    writer = new WaveWriter($"dump{dumpCounter}.wav", staticCapture.WaveFormat);
                }
                writer.Write(e.Data, e.Offset, e.ByteCount);
            }
        }

    }
}