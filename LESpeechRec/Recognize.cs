using Google.Cloud.Speech.V1;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LESpeechRec {

    public class Recognize {

        public static void Main(string[] args) {
            string mydocpath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            File.WriteAllText(mydocpath + @"\speechtext.txt", @"");

            Console.WriteLine(@"Speech Recognition running");
            Console.WriteLine(@"Press Enter to exit");

            StreamingMicRecognizeAsync();

            Console.ReadLine();
        }

        static async void StreamingMicRecognizeAsync() {
            string mydocpath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            if (NAudio.Wave.WaveIn.DeviceCount < 1) {
                Console.WriteLine("Error: No microphone detected!");
            }

            var speech = SpeechClient.Create();
            var streamingCall = speech.StreamingRecognize();
            // Write the initial request with the config.
            await streamingCall.WriteAsync(new StreamingRecognizeRequest() {
                StreamingConfig = new StreamingRecognitionConfig() {
                    Config = new RecognitionConfig() {
                        Encoding = RecognitionConfig.Types.AudioEncoding.Linear16,
                        SampleRateHertz = 16000,
                        LanguageCode = "en",
                    },
                    InterimResults = false,
                    SingleUtterance = false,
                }
            });

            // Print responses as they arrive.
            Task printResponses = Task.Run(async () => {
                while (await streamingCall.ResponseStream.MoveNext(default(CancellationToken))) {
                    foreach (var result in streamingCall.ResponseStream.Current.Results) {
                        foreach (var alternative in result.Alternatives) {
                            //Console.WriteLine(alternative.Transcript);
                            string[] lines = { alternative.Transcript };
                            File.AppendAllLines(mydocpath + @"\speechtext.txt", lines);
                        }
                    }
                }
            });

            object writeLock = new object();
            bool writeMore = true;
            // Read from the microphone and stream to API.
            var waveIn = new NAudio.Wave.WaveInEvent();
            waveIn.DeviceNumber = 0;
            waveIn.WaveFormat = new NAudio.Wave.WaveFormat(16000, 1);
            waveIn.DataAvailable += (object sender, NAudio.Wave.WaveInEventArgs args) => {
                lock (writeLock) {
                    if (!writeMore) return;
                    streamingCall.WriteAsync(
                        new StreamingRecognizeRequest() {
                            AudioContent = Google.Protobuf.ByteString.CopyFrom(args.Buffer, 0, args.BytesRecorded)
                        }).Wait();
                }
            };
            waveIn.StartRecording();

            var Timeout = EasyTimer.SetTimeout(async() => 
            {
                await streamingCall.TryWriteCompleteAsync();
                lock (writeLock) writeMore = false;
                waveIn.StopRecording();
                StreamingMicRecognizeAsync();

            }, 55);
            
            await printResponses;

        }

        public static class EasyTimer {
            public static IDisposable SetTimeout(Action method, int delayInSeconds) {
                System.Timers.Timer timer = new System.Timers.Timer(delayInSeconds*1000);
                timer.Elapsed += (source, e) => {
                    method();
                };

                timer.AutoReset = false;
                timer.Enabled = true;
                timer.Start();

                return timer as IDisposable;
            }
        }
    }
}
