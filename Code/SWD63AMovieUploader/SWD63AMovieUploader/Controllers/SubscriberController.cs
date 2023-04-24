using Microsoft.AspNetCore.Mvc;
using Google.Cloud.PubSub.V1;
using Microsoft.AspNetCore.SignalR;
using SWD63AMovieUploader.Models;
using Newtonsoft.Json;
using System.Net.Http;

namespace SWD63AMovieUploader.Controllers
{
    public class SubscriberController : Controller
    {
        public async Task<int> Index()
        {
            string projectId = "swd63aprogrammingforthecloud";
            string subscriptionId = "transcribe-sub";
            bool acknowledge = true;

            SubscriptionName subscriptionName = SubscriptionName.FromProjectSubscription(projectId, subscriptionId);
            SubscriberClient subscriber = await SubscriberClient.CreateAsync(subscriptionName);
            // SubscriberClient runs your message handle function on multiple
            // threads to maximize throughput.
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

            var httpClient = new HttpClient();

            foreach (var msg in uniqueMessages)
            {
                Movie m = JsonConvert.DeserializeObject<Movie>(msg);
                var uri = new Uri($"https://us-central1-swd63aprogrammingforthecloud.cloudfunctions.net/movie-transcriber-http-function?uri={m.LinkMovie}");
                
                try
                {
                    var response = await httpClient.GetAsync(uri);

                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine("Function called successfully!");
                    }
                    else
                    {
                        Console.WriteLine($"Error: {response.StatusCode}");
                    }
                }
                catch (HttpRequestException e)
                {
                    Console.WriteLine($"Error: {e.Message}");
                }
            }

            return messageCount;
        }
    }
}
