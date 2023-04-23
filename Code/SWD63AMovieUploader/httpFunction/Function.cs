using Google.Cloud.Storage.V1;
using Google.Cloud.Speech.V1;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Functions.Framework;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using Google.Cloud.Firestore;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System;
using Newtonsoft.Json;
using System.Text;
using System.Diagnostics;


namespace httpFunction
{

    public class Function : IHttpFunction
    {
        /// <summary>
        /// Logic for your function goes here.
        /// </summary>
        /// <param name="context">The HTTP context, containing the request and the response.</param>
        /// <returns>A task representing the asynchronous operation.</returns>this.speechClient = SpeechClient.Create();

        public async Task HandleAsync(HttpContext context)
        {
            /*System.Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS",
            @"D:\School\Repositories\ProgrammingForDCloudHBA\Code\SWD63AMovieUploader\httpFunction\swd63aprogrammingforthecloud-ba30695f338b.json");*/

            HttpRequest request = context.Request;
            string uri = request.Query["uri"]; //assuming that in message you received your data
            string audioUri = Conversion(uri);

            FirestoreDb db = FirestoreDb.Create("swd63aprogrammingforthecloud");

            await transcribe(audioUri, db, uri);
            await context.Response.WriteAsync("Hello, Functions Framework.");
        }

        private string Conversion(string uri)
        {
            GoogleCredential credential = GoogleCredential.GetApplicationDefault();
            StorageClient storageClient = StorageClient.Create(credential);

            string bucketName = "pfc_movie_bucket";

            string objectName = Guid.NewGuid() + ".flac";

            var processStartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-i \"{uri}\" -vn -acodec flac -f flac pipe:1",
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
            return $"https://storage.googleapis.com/{bucketName}/{objectName}";
        }

        private async Task transcribe(string flacAudioUri, FirestoreDb db, string storageUri)
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

            var audio = RecognitionAudio.FetchFromUri(flacAudioUri);

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
}
