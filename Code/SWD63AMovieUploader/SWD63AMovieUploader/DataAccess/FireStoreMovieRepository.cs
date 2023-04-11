using Google.Cloud.Firestore;
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

        public async void AddMovie(Movie m)
        {
            await db.Collection("movies").Document().SetAsync(m); 
        }
    }
}
