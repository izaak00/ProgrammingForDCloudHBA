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
    }
}
