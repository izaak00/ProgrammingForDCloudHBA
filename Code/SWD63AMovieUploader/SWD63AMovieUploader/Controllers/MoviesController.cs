using Microsoft.AspNetCore.Mvc;
using SWD63AMovieUploader.DataAccess;
using SWD63AMovieUploader.Models;

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
        public IActionResult Create(Movie m)
        {
            try
            {
                fmr.AddMovie(m);
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
