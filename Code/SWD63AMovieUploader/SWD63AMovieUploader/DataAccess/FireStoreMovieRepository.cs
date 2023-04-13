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
        public async Task AddMovie(Movie m,string name)
        {
            m.Owner = name;
            m.Status = "Not available";
            await db.Collection("movies").Document().SetAsync(m); 
        }
    }
}
