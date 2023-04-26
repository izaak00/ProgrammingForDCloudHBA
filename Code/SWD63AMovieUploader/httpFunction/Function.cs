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
using Microsoft.Extensions.Logging;
using Google.Cloud.PubSub.V1;
using System.Threading;

namespace httpFunction
{
    public class Function : IHttpFunction
    {
        /// <summary>
        /// Logic for your function goes here.
        /// </summary>
        /// <param name="context">The HTTP context, containing the request and the response.</param>
        /// <returns>A task representing the asynchronous operation.</returns>this.speechClient = SpeechClient.Create();

        private readonly ILogger _logger;
        string projectId = "swd63aprogrammingforthecloud";
        string subscriptionId = "transcribe-sub";
        bool acknowledge = true;
        public Function(ILogger<Function> logger)
        {
            _logger = logger;
        }

        public async Task HandleAsync(HttpContext context)
        {
            FirestoreDb db = FirestoreDb.Create(projectId);

            await GetObjectFromPubSub(db);    
            await context.Response.WriteAsync("Successfully Transcribed");
        }

        public async Task GetObjectFromPubSub(FirestoreDb db)
        {
            SubscriptionName subscriptionName = SubscriptionName.FromProjectSubscription(projectId, subscriptionId);
            SubscriberClient subscriber = await SubscriberClient.CreateAsync(subscriptionName);

            int messageCount = 0;

            List<string> messages = new List<string>();

            Task startTask = subscriber.StartAsync((PubsubMessage message, CancellationToken cancel) =>
            {
                string text = System.Text.Encoding.UTF8.GetString(message.Data.ToArray());
                System.Diagnostics.Debug.WriteLine($"Message {message.MessageId}: {text}");
                Console.WriteLine($"Message {message.MessageId}: {text}");
                messages.Add($"{text}");
                Interlocked.Increment(ref messageCount);
                return Task.FromResult(acknowledge ? SubscriberClient.Reply.Ack : SubscriberClient.Reply.Nack);
            });
            // Run for 5 seconds.
            await Task.Delay(5000);
            await subscriber.StopAsync(CancellationToken.None);
            // Lets make sure that the start task finished successfully after the call to stop.
            await startTask;

            List<string> uniqueMessages = messages.Distinct().ToList();

            if (uniqueMessages == null)
            {
                _logger.LogWarning("There are no messages in pub/sub");
            }

            _logger.LogInformation("There is a message in pub/sub");

            foreach (var msg in uniqueMessages)
            {
                Movie m = JsonConvert.DeserializeObject<Movie>(msg);      
                
                string audioUri = Conversion(m.LinkMovie);
                await transcribe(audioUri, db, m.LinkMovie);
            }
        }
        
        private string Conversion(string uri)
        {
            GoogleCredential credential = GoogleCredential.GetApplicationDefault();
            StorageClient storageClient = StorageClient.Create(credential);

            string bucketName = "pfc_movie_bucket";
             _logger.LogInformation($"Bucket name is: {bucketName}");
            string objectName = Guid.NewGuid() + ".flac";
             _logger.LogInformation($"object name is: {objectName}");
            try
            {
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

                        if (audioBytes == null)
                        {
                             _logger.LogWarning("audio bytes is empty");
                        }

                        // Create a new MemoryStream from the byte array
                        MemoryStream audioMemoryStream = new MemoryStream(audioBytes);

                        // Create an IFormFile object from the MemoryStream
                        IFormFile file = new FormFile(audioMemoryStream, 0, audioBytes.Length, null, objectName);
                        Stream fileStream = file.OpenReadStream();

                        storageClient.UploadObject(bucketName, objectName, null, fileStream);
                    }
                }
            }
            catch (Exception ex)
            {
                 _logger.LogError(ex, "Error occurred during conversion from .mp4 to ffmpeg");
            }
            
             _logger.LogInformation("File successfully converted to flac");
            return $"https://storage.googleapis.com/{bucketName}/{objectName}";
        }

        private async Task transcribe(string flacAudioUri, FirestoreDb db, string storageUri)
        {
             _logger.LogInformation($"Flac audio uri is {flacAudioUri}");
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
             _logger.LogInformation("Found movie with that particular Link from bucket");
            
            if (allMoviesQuery == null)
            {
                 _logger.LogError("Did not find movie with that particular Link from bucket");
            }

            QuerySnapshot allMoviesQuerySnapshot = await allMoviesQuery.GetSnapshotAsync();

            DocumentSnapshot documentSnapshot = allMoviesQuerySnapshot.Documents.FirstOrDefault();
            if (documentSnapshot != null)
            {
                 _logger.LogInformation("Successfully found document. Document snapshot not null");
                string id = documentSnapshot.Id;
                DocumentReference vidRef = db.Collection("movies").Document(id);

                try
                {
                    await vidRef.UpdateAsync(new Dictionary<string, object>
                    {
                        { "Transcription", resultJson },
                        {"Status","Available"}
                    });
                     _logger.LogInformation("Successfully added fields, transcription and changed status to available");
                }
                catch (Exception ex)
                {
                    
                     _logger.LogError(ex,"Failed to add fields, transcription and status");
                }           
            }
        }      
    }
}
