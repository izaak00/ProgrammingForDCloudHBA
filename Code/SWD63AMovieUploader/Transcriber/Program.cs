using Google.Cloud.Speech.V1;
using Google.Cloud.Storage.V1;
using Google.Cloud.Firestore;
using Google.Apis.Auth.OAuth2;
using NReco.VideoConverter;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System.Text;
using System.Diagnostics;
using System;

class Program
{
    static async Task Main(string[] args)
    {
        // Set up the credentials for the Google Cloud Storage client
        System.Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS",
        @"C:\Users\izaak\Desktop\ConsoleApp1\ConsoleApp1\swd63aprogrammingforthecloud-ba30695f338b.json");

        FirestoreDb db = FirestoreDb.Create("swd63aprogrammingforthecloud");

        GoogleCredential credential = GoogleCredential.GetApplicationDefault();
        StorageClient storageClient = StorageClient.Create(credential);

        string storageUri = "https://storage.googleapis.com/pfc_movie_bucket/Talking.mp4";
        string bucketName = "pfc_movie_bucket";
        string objectName = Guid.NewGuid() + ".flac";

        try
        {
            // set up FFmpeg process
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-i \"{storageUri}\" -vn -acodec flac -f flac pipe:1",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = processStartInfo })
            {
                // start FFmpeg process
                process.Start();

                // read output into memory stream
                using (var audioStream = new MemoryStream())
                {
                    process.StandardOutput.BaseStream.CopyTo(audioStream);
                    audioStream.Seek(0, SeekOrigin.Begin);

                    // Create a byte array from the MemoryStream contents
                    byte[] audioBytes = audioStream.ToArray();

                    // Create a new MemoryStream from the byte array
                    MemoryStream audioMemoryStream = new MemoryStream(audioBytes);

                    // Create an IFormFile object from the MemoryStream
                    IFormFile file = new FormFile(audioMemoryStream, 0, audioBytes.Length, null, objectName);
                    Stream fileStream = file.OpenReadStream();

                    storageClient.UploadObject(bucketName, objectName, null, fileStream);
                }
            }
           
            string flacAudioUri = $"gs://{bucketName}/{objectName}";

            Console.WriteLine($"Conversion successful: {flacAudioUri}");
            await transcribe(flacAudioUri,db,storageUri);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Conversion failed: {ex.Message}");
        }

        Console.ReadLine();
    }


    static async Task transcribe(string flacAudioUri, FirestoreDb db, string storageUri)
    {

        var speech = SpeechClient.Create();
        var config = new RecognitionConfig
        {
            Encoding = RecognitionConfig.Types.AudioEncoding.Flac,
            SampleRateHertz = 44100,
            AudioChannelCount = 2,
            LanguageCode = LanguageCodes.English.UnitedStates,
            EnableWordTimeOffsets = true
        };

        var audio = RecognitionAudio.FromStorageUri(flacAudioUri);

        var response = speech.Recognize(config, audio);

        var builder = new StringBuilder();

        foreach (var result in response.Results)
        {
            foreach (var alternative in result.Alternatives)
            {
                var json = JsonConvert.SerializeObject(alternative);
                builder.AppendLine(json);
            }
        }

        string resultJson = builder.ToString();

        Query allMoviesQuery = db.Collection("movies").WhereEqualTo("LinkMovie", storageUri);
        QuerySnapshot allMoviesQuerySnapshot = await allMoviesQuery.GetSnapshotAsync();

        DocumentSnapshot documentSnapshot = allMoviesQuerySnapshot.Documents.FirstOrDefault();
        if (documentSnapshot != null)
        {
            string id = documentSnapshot.Id;
            DocumentReference vidRef = db.Collection("movies").Document(id);

            await vidRef.UpdateAsync(new Dictionary<string, object>
            {
                { "Transcription", resultJson },
                {"Status","Available"}
            });
        }

    }


}