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
        public async void AddMovie(Movie m,string name)
        {
            m.Owner = name;
            await db.Collection("movies").Document().SetAsync(m); 
        }
    }
}
