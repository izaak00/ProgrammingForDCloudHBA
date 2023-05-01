﻿using Google.Cloud.Firestore;

namespace SWD63AMovieUploader.Models
{
    [FirestoreData]
    public class Movie
    {
        private DateTime _dateTime;

        [FirestoreProperty]
        public string Title { get; set; }
        [FirestoreProperty]
        public DateTime DateTimeUtc 
        { 
          get { return _dateTime.ToUniversalTime(); }
          set { _dateTime = value.ToUniversalTime(); } 
        }
        [FirestoreProperty]
        public string Owner 
        { get; set; }
        [FirestoreProperty]
        public string Thumbnail { get; set; }
        [FirestoreProperty]
        public string Status { get; set; }

        [FirestoreProperty]
        public string LinkMovie { get; set; }

        [FirestoreProperty]
        public string Transcription { get; set; }

        [FirestoreProperty]
        public string SRTStatus { get; set; }
    }
}
