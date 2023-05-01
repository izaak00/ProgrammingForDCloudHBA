using Google.Cloud.Functions.Framework;
using Google.Cloud.Storage.V1;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Google.Cloud.Firestore;
using Newtonsoft.Json;
using Google.Cloud.PubSub.V1;
using System.Threading;

namespace httpFunctionSRT;

public class Function : IHttpFunction
{
    /// <summary>
    /// Logic for your function goes here.
    /// </summary>
    /// <param name="context">The HTTP context, containing the request and the response.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    
    private readonly ILogger _logger;
    string subscriptionId = "srt-sub";
    string projectId = "swd63aprogrammingforthecloud";
    bool acknowledge = true;
    public Function(ILogger<Function> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(HttpContext context)
    {
        FirestoreDb db = FirestoreDb.Create(projectId);

        await GetObjectFromPubSub(db);
        await context.Response.WriteAsync("SRT Successfull");
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
            string uri = JsonConvert.DeserializeObject<string>(msg);      
            
            await ConnectToFirestore(db, uri);
        }
    }

    public async Task ConnectToFirestore(FirestoreDb db, string uri)
    {
        string bucketName = "pfc_movie_bucket";
        string objectName = Guid.NewGuid() + ".srt";

        Query allMoviesQuery = db.Collection("movies").WhereEqualTo("LinkMovie", uri);
        QuerySnapshot allMoviesQuerySnapshot = await allMoviesQuery.GetSnapshotAsync();
        DocumentSnapshot documentSnapshot = allMoviesQuerySnapshot.Documents.FirstOrDefault();

        if (documentSnapshot != null)
        {
            _logger.LogInformation("Found movie with given url");

            string Transcription = documentSnapshot.GetValue<string>("Transcription");

            if(Transcription != null)
            {
                _logger.LogInformation("Transcription not empty");
            }
            else
            {
                _logger.LogError("Transcription is empty");
            }

            ConvertToSrtAndUploadToBucket(Transcription, bucketName, objectName); 
     
            try
            {
                string id = documentSnapshot.Id;
                _logger.LogInformation($"Document id is {id}");
                var docRef = db.Collection($"movies/{id}/srt").Document();
                
                if (docRef != null)
                {
                    _logger.LogInformation("docref not null. Successfully created document");
                }
                else 
                {
                    _logger.LogError("docref null. Did not create document");
                }
                var data = new Dictionary<string, object>
                {
                    { "LinkToBucketForSRT", $"https://storage.googleapis.com/pfc_movie_bucket/{objectName}" },
                };

                await docRef.SetAsync(data);

                var srtAvailability = db.Collection("movies").Document(id);
                try
                {
                    await srtAvailability.UpdateAsync(new Dictionary<string, object>
                    {
                        { "SRTStatus", "Available" },
                    });
                     _logger.LogInformation("Successfully added field, changed SRTStatus to available");
                }
                catch (Exception ex)
                {
                    
                     _logger.LogError(ex,"Failed to update status");
                }           
            }                
            catch (Exception ex)
            {
                
                    _logger.LogError(ex,"Failed to add fields, transcription and status");
            }                
        }
        else 
        {
            _logger.LogError("Could not find movie. Document snapshot is empty.");
        }      
    }

    public static void ConvertToSrtAndUploadToBucket(string transcription, string bucketName, string objectName)
    {
        dynamic transcriptData = JsonConvert.DeserializeObject(transcription);
        string[] lines = transcription.Split(new string[] { ".", "!", "?" }, StringSplitOptions.RemoveEmptyEntries);
        StringBuilder srtBuilder = new StringBuilder();
        string timestampFormat = "HH:mm:ss,fff";

        int wordIndex = 1;

        foreach (string line in lines)
        {
            string[] words = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> wordList = new List<string>(words);

            foreach (var word in transcriptData.Words)
            {     
                if (word.Word != null && wordList.Contains(word.Word.ToString()))
                {
                    // Extract the start and end times of the word
                    DateTime startTime = new DateTime().Add(TimeSpan.FromSeconds((double)word.StartTime.Seconds + (double)word.StartTime.Nanos / 1000000000));
                    DateTime endTime = new DateTime().Add(TimeSpan.FromSeconds((double)word.EndTime.Seconds + (double)word.EndTime.Nanos / 1000000000));
                    string startTimestamp = startTime.ToString(timestampFormat);
                    string endTimestamp = endTime.ToString(timestampFormat);

                    srtBuilder.AppendLine(wordIndex.ToString());
                    srtBuilder.AppendLine($"{startTimestamp} --> {endTimestamp}");
                    srtBuilder.AppendLine(word.Word.ToString());
                    srtBuilder.AppendLine();

                    // Append the word index, start and end timestamps, and the word text to the SRT file
                    wordIndex++;
                }
            }
            // Add a new line after each sentence ending with ".", "!", or "?"
            if (line.EndsWith(".") || line.EndsWith("!") || line.EndsWith("?"))
            {
                srtBuilder.AppendLine();
            }
        }

        //Upload the SRT content to the Google bucket.
        StorageClient storage = StorageClient.Create();
        using (MemoryStream stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(srtBuilder.ToString())))
        {
            storage.UploadObject(bucketName, objectName, null, stream);
        }
    }

}
