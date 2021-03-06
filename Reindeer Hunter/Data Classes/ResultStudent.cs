﻿using FileHelpers;

namespace Reindeer_Hunter.Data_Classes
{
    /// <summary>
    /// Class for recording results from a .csv file.
    /// </summary>
    [DelimitedRecord(",")]
    [IgnoreFirst(1)]
    public class ResultStudent
    {
        [FieldNullValue("")]
        public string TimeStamp;

        [FieldNotEmpty]
        public string First;

        [FieldNotEmpty]
        public string Last;

        [FieldNotEmpty]
        public int Homeroom;

        [FieldOptional]
        [FieldNullValue(null)]
        public string Id;
    }
}
