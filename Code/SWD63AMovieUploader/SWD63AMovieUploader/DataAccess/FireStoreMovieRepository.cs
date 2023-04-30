using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Authorization;
using SWD63AMovieUploader.Models;

namespace SWD63AMovieUploader.DataAccess
{
    public class FireStoreMovieRepository
    {
        FirestoreDb db;
        public FireStoreMovieRepository(string project)
        {
            db = FirestoreDb.Create(project);
        }

        [Authorize]
        public async Task AddMovie(Movie m,string email)
        {
            m.Owner = email;        
            m.Status = "Not available";
            await db.Collection("movies").Document().SetAsync(m); 
        }

        [Authorize]
        public async Task<List<Movie>> GetMovies(string email)
        {
            List<Movie> movies = new List<Movie>();

            Query allMoviesQuery = db.Collection("movies");
            Query movieLinkedToAccount = allMoviesQuery.WhereEqualTo("Owner", email);
            QuerySnapshot allBooksQuerySnapshot = await movieLinkedToAccount.GetSnapshotAsync();
            foreach (DocumentSnapshot documentSnapshot in allBooksQuerySnapshot.Documents)
            {
                Movie movie = documentSnapshot.ConvertTo<Movie>();

                movies.Add(movie);
            }
            return movies;
        }

        [Authorize]
        public async Task AddDownloads(string owner, string LinkMovie)
        {
            var docIdOfTheMovieBeingDownloaded = await GetMovieDocumentId(LinkMovie);

            var docRef = db.Collection($"movies/{docIdOfTheMovieBeingDownloaded}/downloads").Document();

            var data = new Dictionary<string, object>
            {
                { "owner", owner },
                { "timestamp", DateTime.UtcNow }
            };

            await docRef.SetAsync(data);
        }

        [Authorize]
        public async Task<string> DownloadSRT(string LinkMovie)
        {
            var docId = await GetMovieDocumentId(LinkMovie);

            // Get a reference to the collection of srt documents
            CollectionReference srtCollection = db.Collection($"movies/{docId}/srt");

            // Get a snapshot of all documents in the collection
            QuerySnapshot snapshot = await srtCollection.GetSnapshotAsync();

            if (snapshot.Documents.Count > 0)
            {
                DocumentSnapshot docSnapshot = snapshot.Documents[0];
                if (docSnapshot.ContainsField("LinkToBucketForSRT"))
                {
                    return docSnapshot.GetValue<string>("LinkToBucketForSRT");
                }
                else
                {
                    Console.WriteLine("The document does not contain the field LinkToBucketForSRT.");
                }
            }
            else
            {
                Console.WriteLine("There are no documents in the srt subcollection.");
            }

            return "";
        }


        public async Task<string> GetMovieDocumentId(string LinkMovie)
        {
            Query allMoviesQuery = db.Collection("movies").WhereEqualTo("LinkMovie", LinkMovie);
            QuerySnapshot allMoviesQuerySnapshot = await allMoviesQuery.GetSnapshotAsync();

            DocumentSnapshot documentSnapshot = allMoviesQuerySnapshot.Documents.FirstOrDefault();
            return documentSnapshot.Id;
        }
    }
}
