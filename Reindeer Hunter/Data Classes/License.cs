﻿using Reindeer_Hunter.Hunt;

namespace Reindeer_Hunter.Data_Classes
{
    public class License
    {
        public string First1 { get; private set; } = "";
        public string Last1 { get; private set; } = "";
        /// <summary>
        /// Id of the the student in the license.
        /// </summary>
        public string Id1 { get; private set; }
        private string First2 { get; set; } = "";
        private string Last2 { get; set; } = "";
        public int Homeroom1 { get; set; } = 0;
        public int Homeroom2 { get; set; } = 0;
        public string Date { get; set; } = "";
        public long Round { get; set; } = 0;
        public int Grade { get; set; } = 0;

        public string Student1Field
        {
            get
            {
                return string.Format("{0} {1} ({2})", First1, Last1, Homeroom1);
            }
        }

        public string Student2Field
        {
            get
            {
                return string.Format("{0} {1} ({2})", First2, Last2, Homeroom2);
            }
        }

        public static License[] CreateFromMatch(Match match, string date)
        {
            int numLicensesTomake;
            string first2;
            string last2;

            if (School.IsPassMatch(match))
            {
                numLicensesTomake = 1;
                first2 = "Move";
                last2 = "on";
            }
            else
            {
                numLicensesTomake = 2;
                first2 = match.First2;
                last2 = match.Last2;
            }

            License[] returnable = new License[numLicensesTomake];

            returnable[0] = new License
            {
                First1 = match.First1,
                First2 = first2,
                Last1 = match.Last1,
                Last2 = last2,
                Homeroom1 = match.Home1,
                Homeroom2 = match.Home2,
                Round = match.Round,
                Date = date,
                Grade = match.Grade1,
                Id1 = match.Id1
            };

            // Don't do student 2 if it is a pass match, since student2 is the passer.
            if (numLicensesTomake == 2)
            {
                returnable[1] = new License
                {
                    First1 = match.First2,
                    First2 = match.First1,
                    Last1 = match.Last2,
                    Last2 = match.Last1,
                    Id1 = match.Id2,
                    Homeroom1 = match.Home2,
                    Homeroom2 = match.Home1,
                    Round = match.Round,
                    Grade = match.Grade2,
                    Date = date
                };
            }

            return returnable;
        }
    }
}
