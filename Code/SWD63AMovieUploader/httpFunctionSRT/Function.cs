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

namespace httpFunctionSRT;

public class Function : IHttpFunction
{
    /// <summary>
    /// Logic for your function goes here.
    /// </summary>
    /// <param name="context">The HTTP context, containing the request and the response.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    
    private readonly ILogger _logger;
    
    public Function(ILogger<Function> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(HttpContext context)
    {
        // Set up the credentials for the Google Cloud Storage client
        System.Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS",
        @"D:\School\Repositories\ProgrammingForDCloudHBA\Code\SWD63AMovieUploader\httpFunctionSRT\swd63aprogrammingforthecloud-ba30695f338b.json");
        
        HttpRequest request = context.Request;
        string uri = request.Query["uri"];

        string projectId = "swd63aprogrammingforthecloud";

        FirestoreDb db = FirestoreDb.Create(projectId);

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
                //_logger.LogInformation("Successfully added fields, created a new nested collection with the link inside of SRT");
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
        await context.Response.WriteAsync("Hello, Functions Framework.");
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
        using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(srtBuilder.ToString())))
        {
            storage.UploadObject(bucketName, objectName, null, stream);
        }
    }

}
