using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Mvc;
using NuGet.DependencyResolver;
using SWD63AMovieUploader.DataAccess;
using SWD63AMovieUploader.Models;
using System.Security.AccessControl;
using System.Security.Claims;

namespace SWD63AMovieUploader.Controllers
{
    public class MoviesController : Controller
    {
        FireStoreMovieRepository fmr;
        public MoviesController(FireStoreMovieRepository _fmr)
        {
            fmr = _fmr;
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public async Task <IActionResult> Create(Movie m, IFormFile file, [FromServices] IConfiguration config)
        {
            try
            {
                string objectName = Guid.NewGuid() + System.IO.Path.GetExtension(file.FileName);
                //-----------------adding the movie to cloud storage-----------------
                string bucketName = config["bucket"];
                string projectId = config["project"];

                var storage = StorageClient.Create();
                using var fileStream = file.OpenReadStream();
                storage.UploadObject(bucketName, objectName, null, fileStream);

                m.Link = $"https://storage.googleapis.com/{bucketName}/{objectName}";

                //-----------------end adding the movie to cloud storage-----------------
                string name = User.Identity.Name;
                
                // adding rest of info in firestore
                await fmr.AddMovie(m,name);

                TempData["success"] = "Movie added successfully";
            }
            catch (Exception ex)
            {
                //logging
                TempData["error"] = "Error occured.Did not add Movie";
            }
            return View();
        }
    }
}
