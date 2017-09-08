﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FileHelpers;

namespace Reindeer_Hunter
{
    /// <summary>
    /// Class for recording results from a .csv file.
    /// </summary>
    [DelimitedRecord(",")]
    [IgnoreFirst(1)]
    public class ResultStudent
    {
        [FieldNotEmpty]
        public string First;

        [FieldNotEmpty]
        public string Last;

        [FieldNotEmpty]
        public int Homeroom;

        [FieldOptional]
        [FieldNullValue(0)]
        public int Id;
    }
}