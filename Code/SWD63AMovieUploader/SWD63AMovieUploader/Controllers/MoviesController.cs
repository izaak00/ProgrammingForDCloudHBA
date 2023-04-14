using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Authorization;
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
        
        [Authorize]
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [Authorize]
        [HttpPost]
        public async Task <IActionResult> Create(Movie m, IFormFile thumbnailFile, IFormFile movieFile, [FromServices] IConfiguration config)
        {
            try
            {
                string objectName = Guid.NewGuid() + System.IO.Path.GetExtension(movieFile.FileName);
                //-----------------adding the movie to cloud storage-----------------
                string bucketName = config["bucket"];
                string projectId = config["project"];

                var storage = StorageClient.Create();
                using var fileStream = movieFile.OpenReadStream();
                storage.UploadObject(bucketName, objectName, null, fileStream);

                m.LinkMovie = $"https://storage.googleapis.com/{bucketName}/{objectName}";

                //-----------------end adding the movie to cloud storage-----------------
                var email = User.FindFirstValue(ClaimTypes.Email);

                //-----------------adding thumbnail to bucket-----------------
                string thumbnailObjName = Guid.NewGuid() + System.IO.Path.GetExtension(thumbnailFile.FileName);     
                using var thumbnailFileStream = thumbnailFile.OpenReadStream();
                storage.UploadObject(bucketName, thumbnailObjName, null, thumbnailFileStream);

                m.Thumbnail = $"https://storage.googleapis.com/{bucketName}/{thumbnailObjName}";
                //--------------end of adding thumbnail to bucket--------------

                // adding rest of info in firestore
                await fmr.AddMovie(m, email);

                TempData["success"] = "Movie added successfully";
            }
            catch (Exception ex)
            {
                //logging
                TempData["error"] = "Error occured.Did not add Movie";
            }
            return View();
        }

        [Authorize]
        public async Task<IActionResult> Index()
        {
            var list = await (fmr.GetMovies(User.FindFirstValue(ClaimTypes.Email)));
            return View(list);
        }
    }
}
