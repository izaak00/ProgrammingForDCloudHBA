using System.Diagnostics;
using Google.Cloud.Speech.V1;
using System;
using Google.Cloud.Storage.V1;
using System.Security.AccessControl;
using Google.Apis.Auth.OAuth2;
using NReco.VideoConverter;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.AspNetCore.Http;

class Program
{
    static void Main(string[] args)
    {
        // Set up the credentials for the Google Cloud Storage client
        System.Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS",
        @"C:\Users\izaak\Desktop\ConsoleApp1\ConsoleApp1\swd63aprogrammingforthecloud-ba30695f338b.json");

        GoogleCredential credential = GoogleCredential.GetApplicationDefault();
        StorageClient storageClient = StorageClient.Create(credential);

        string outputFilePath = @"C:\Users\izaak\Desktop\ConsoleApp1\ConsoleApp1\Videos\audio.flac";
        string storageUri = "https://storage.googleapis.com/pfc_movie_bucket/Talking.mp4";
        string bucketName = "pfc_movie_bucket";

        try
        {
            var ffMpeg = new NReco.VideoConverter.FFMpegConverter();

            // Create a MemoryStream to store the output audio
            MemoryStream audioStream = new MemoryStream();

            ffMpeg.ConvertMedia(storageUri, audioStream, Format.flac);

            // Create a byte array from the MemoryStream contents
            byte[] audioBytes = audioStream.ToArray();

            // Create a new MemoryStream from the byte array
            MemoryStream audioMemoryStream = new MemoryStream(audioBytes);

            // Create an IFormFile object from the MemoryStream
            IFormFile file = new FormFile(audioMemoryStream, 0, audioBytes.Length, null, "audio.flac");

            //IFormFile file = new FormFile(File.OpenRead(outputFilePath), 0, new FileInfo(outputFilePath).Length, null, Path.GetFileName(outputFilePath));

            Stream fileStream = file.OpenReadStream();

            storageClient.UploadObject(bucketName, "Audio.flac", null, fileStream);
       

            //ConvertMovieToAudio(storageUri, outputFilePath, bucketName);
            Console.WriteLine($"Conversion successful: {outputFilePath}");
            transcribe(outputFilePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Conversion failed: {ex.Message}");
        }

        Console.ReadLine();
    }

    static void RunFFmpegCommand(string arguments)
    {
        string ffmpegPath = @"C:\Users\izaak\Desktop\ConsoleApp1\ConsoleApp1\ffmpeg\bin\ffmpeg.exe"; // Change this to match the path to your FFmpeg executable
        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.FileName = ffmpegPath;
        startInfo.Arguments = arguments;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;

        using (Process process = new Process())
        {
            process.StartInfo = startInfo;
            process.Start();
            process.WaitForExit();
        }
    }

    static void transcribe(string outputFilePath)
    {
        //System.Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS",
        //@"C:\Users\izaak\Desktop\ConsoleApp1\swd63aprogrammingforthecloud-ba30695f338b.json");

        var speech = SpeechClient.Create();
        var config = new RecognitionConfig
        {
            Encoding = RecognitionConfig.Types.AudioEncoding.Flac,
            SampleRateHertz = 44100,
            AudioChannelCount = 2,
            LanguageCode = LanguageCodes.English.UnitedStates
        };

        //var audio = RecognitionAudio.FromStorageUri("gs://cloud-samples-tests/speech/brooklyn.flac");
        var audio = RecognitionAudio.FromFile(outputFilePath);

        var response = speech.Recognize(config, audio);

        using (StreamWriter file = new StreamWriter(@"C:\Users\izaak\Desktop\ConsoleApp1\ConsoleApp1\Videos\Transcription.txt"))
        {
            foreach (var result in response.Results)
            {
                foreach (var alternative in result.Alternatives)
                {
                    //Console.WriteLine(alternative.Transcript);
                    //File.WriteAllText(@"C:\Users\izaak\Desktop\ConsoleApp1\Videos\Transcription.txt", alternative.Transcript);
                    //upload the text in firestore as a string
                    file.WriteLine(alternative.Transcript);
                }
            }
        }
    }

}