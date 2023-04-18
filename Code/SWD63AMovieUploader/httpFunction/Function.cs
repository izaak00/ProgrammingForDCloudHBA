using Google.Cloud.Storage.V1;
using Google.Cloud.Speech.V1;
using Google.Apis.Storage.v1.Data;
using Google.Cloud.Functions.Framework;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.Linq;
using System.IO;

namespace httpFunction;

public class Function : IHttpFunction
{
    /// <summary>
    /// Logic for your function goes here.
    /// </summary>
    /// <param name="context">The HTTP context, containing the request and the response.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private readonly StorageClient storageClient;
    private readonly SpeechClient speechClient;

    public Function()
    {
        // Create a new instance of the Cloud Storage client
        this.storageClient = StorageClient.Create();

        // Create a new instance of the Speech-to-Text client
        this.speechClient = SpeechClient.Create();
    }

    public async Task HandleAsync(HttpContext context)
    {
        System.Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS",
        @"swd63aprogrammingforthecloud-ba30695f338b.json");

        HttpRequest request = context.Request;
        string uri = request.Query["uri"]; //assuming that in message you received your data

        await ConvertAndTranscribeAsync(uri);
        await context.Response.WriteAsync("Hello, Functions Framework.");
    }

    public async Task<string> ConvertAndTranscribeAsync(string uri)
    {
        string bucketName = "pfc_movie_bucket";
        string fileName = "89770f3f-24ba-4ac2-beea-9f9388339fd1.mp4";

        // Download the MP4 file from Cloud Storage
        var stream = new MemoryStream();
        await storageClient.DownloadObjectAsync(bucketName, fileName, stream);

        // Convert the MP4 to FLAC using FFmpeg
        var process = new System.Diagnostics.Process();
        process.StartInfo.FileName = "ffmpeg";
        process.StartInfo.Arguments = $"-i pipe:0 -vn -acodec flac -f flac -";
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardInput = true;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;

        process.Start();
        stream.CopyTo(process.StandardInput.BaseStream);
        process.StandardInput.Flush();
        process.StandardInput.Close();
        var flacStream = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(flacStream);
        flacStream.Position = 0;

        // Upload the FLAC file to Google Cloud Storage
        var flacFileName = $"{Path.GetFileNameWithoutExtension(fileName)}.flac";
        await storageClient.UploadObjectAsync(bucketName, flacFileName, null, flacStream);

        // Create a recognition config
        var config = new RecognitionConfig
        {
            Encoding = RecognitionConfig.Types.AudioEncoding.Flac,
            LanguageCode = "en-US",
        };

        // Create a recognition audio object
        var audio = RecognitionAudio.FromStorageUri($"gs://{bucketName}/{flacFileName}");

        // Call the Speech-to-Text API to transcribe the audio
        var response = await speechClient.RecognizeAsync(config, audio);

        // Get the transcription from the response
        var transcription = response.Results.FirstOrDefault()?.Alternatives.FirstOrDefault()?.Transcript;

        return transcription;
    }
    
}
