using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Newtonsoft.Json;
using SWD63AMovieUploader.Models;

namespace SWD63AMovieUploader.DataAccess
{
    public class PubsubTranscriberRepository
    {
        TopicName topicName;
        public PubsubTranscriberRepository(string projectId)
        {
            try
            {
                PublisherServiceApiClient publisher = PublisherServiceApiClient.Create();
                topicName = TopicName.FromProjectTopic(projectId, "transcribe");

                if (publisher.GetTopic(topicName) == null)
                {
                    Topic topic = null;
                    topic = publisher.CreateTopic(topicName);
                }
            }
            catch (Exception ex)
            {
                //log
                throw ex;
            }
            
        }

        public async Task<string> PushMessage(Movie m)
        {
            PublisherClient publisher = await PublisherClient.CreateAsync(topicName);

            var movie = JsonConvert.SerializeObject(m);

            var pubsubMessage = new PubsubMessage
            {
                // The data is any arbitrary ByteString. Here, we're using text.
                Data = ByteString.CopyFromUtf8(movie),
                // The attributes provide metadata in a string-to-string dictionary.
                Attributes =
                {
                    { "priority", "low" },    
                }
            };
            string message = await publisher.PublishAsync(pubsubMessage);
            return message;
        }
    }
}
