﻿using Reindeer_Hunter.Data_Classes;
using Reindeer_Hunter.Subsystems.Manager;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Reindeer_Hunter.Hunt
{
    /// <summary>
    /// This class will be in charge of evereything. Holds the lists of students and other information.
    /// </summary>
    public class School
    {
        // Data Locations withing the misc data
        private static readonly string TopMatchKey = "TopMatch";
        private static readonly string RoundNoKey = "RoundNo";
        private static readonly string IsFFAKey = "IsFFA";
        private static readonly string EndDateKey = "EndDate";
        private static readonly string FormKey = "Form";
        private static readonly string VersionKey = "Version";


        // Event raised when something about matches is changed/updated
        public event EventHandler<Match[]> MatchChangeEvent;

        // Called when the round number is increased.
        public event EventHandler RoundIncreased;

        // Called when students are imported
        public event EventHandler StudentsImported;

        // This dictionary will contain all data for the program
        protected Hashtable data;
        protected Dictionary<string, Student> student_directory;

        // Students with keys of "Firstname + " " + Lastname"
        protected Hashtable studentName_directory;

        // Students with keys of "homeroom#"
        protected Dictionary<int, List<Student>> homeroom_directory;

        public int NumInStudents
        {
            get
            {
                return GetAllParticipatingStudents().Count;
            }
        }

        public int TotalNumStudents
        {
            get
            {
                return student_directory.Count;
            }
        }

        public int NumOpenMatches
        {
            get
            {
                return GetOpenMatchesList().Count;
            }
        }

        public string FormURL
        {
            get
            {
                return misc[FormKey].ToString();
            }

            set
            {
                misc[FormKey] = value;
                Save();
            }
        }

        public int Numgrades
        {
            get
            {
                return GetAllStudentsByGrade().Count;
            }
        }

        public int NumPassMatches
        {
            get
            {
                int num = 0;

                foreach (Match match in match_directory.Values)
                {
                    if (IsPassMatch(match)) num += 1;
                }

                return num;
            }
        }

        /// <summary>
        /// Simple function to get the number of matches on file that are either open or closed.
        /// </summary>
        /// <returns></returns>
        public int GetNumMatchesGenerated()
        {
            return match_directory.Count;
        }

        protected static Dictionary<string, Match> match_directory;
        protected static Hashtable misc;

        public DataFileIO DataFile { get; set; }

        private readonly string STUDENTKEY = "students";

        /// <summary>
        /// Determines if we're ready to go to the next round
        /// </summary>
        public bool IsReadyForNextRound
        {
            get
            {
                if (GetOpenMatchesList().Count() == 0) return true;
                else return false;
            }
        }

        public School()
        {
            try
            {
                // Make the new dataFile string and get the data from the data file.
                DataFile = new DataFileIO();
                data = DataFile.Read();
            }
            catch (ProgramNotSetup)
            {
                // Set up the program if it has no data yet.
                FirstTimeSetup();
                data = DataFile.Read();
            }

            // Declare these for simplicity and ease of use.
            try
            {
                student_directory = (Dictionary<string, Student>)data[STUDENTKEY];
            }
            // If we're unable to cast this to string properly, we're going to need to convert.
            catch (InvalidCastException)
            {
                // This happens because of compatibility issues.
                Dictionary<int, Student> oldFormat = (Dictionary<int, Student>)data[STUDENTKEY];
                student_directory = new Dictionary<string, Student>();
                foreach (KeyValuePair<int, Student> pair in oldFormat)
                {
                    student_directory.Add(pair.Key.ToString(), pair.Value);
                }
            }

            // Fix any problems that occurred during save.
            foreach (Student student in student_directory.Values)
            {
                /* The first value is null when the student has not yet participated in any matches later. 
                 * This is a problem later. */
                if (student.MatchesParticipated.Count > 0 && student.MatchesParticipated[0] == null) student.MatchesParticipated.RemoveAt(0);
            }

            CreateStudentDirs();
            match_directory = (Dictionary<string, Match>)data["matches"];
            misc = (Hashtable)data["misc"];


            #region Compatibility Check
            /* 
             * Keep compatibility with older versions of the program by ensuring that
             * the older data format is upgraded.
             * 
             * Catch any sort of errors and quit when they occur.
             */
            try
            {
                CheckCompatibility();
            }
            catch (Exception)
            {
                DataFile.QuitApplication();
            }
            #endregion
        }

        /// <summary>
        /// Method to ensure that the datafile is compatible with this version
        /// of the application.
        /// 
        /// Upgrades the datafile if it isn't.
        /// </summary>
        private void CheckCompatibility()
        {
            bool changed = false;

            // If the version of the program data file is the same as the current version, no upgrades necessary.
            if (misc.ContainsKey(VersionKey) && ((string)misc[VersionKey]).Equals(StartupWindow.APPLICATION_VERSION)) return;

            string rawFileVersion = (string)misc[VersionKey];
            if (!misc.ContainsKey(VersionKey))
            {
                misc[VersionKey] = StartupWindow.APPLICATION_VERSION;
                changed = true;
            }
            else if (!((string)misc[VersionKey]).Equals(StartupWindow.APPLICATION_VERSION))
            {
                misc[VersionKey] = StartupWindow.APPLICATION_VERSION;
                changed = true;
            }

            string[] fileVersion = StartupWindow.ParseBuildInformation(rawFileVersion);
            int fileBuildNo = int.Parse(fileVersion[fileVersion.Length - 1]);

            // Make sure that the file isn't for an even more recent build.
            if (fileBuildNo >  StartupWindow.BUILD_VERSION)
            {
                // Show error message.
                MessageBox.Show(string.Format("The data file in use is for a more recent version of the application." +
                    "\nDataFile version: {0}\nApplication Version: {1}", rawFileVersion, StartupWindow.APPLICATION_VERSION), 
                    "Error - Incompatible DataFile",
                    MessageBoxButton.OK, MessageBoxImage.Error);


                throw new Exception("The version of the data file is greater than that of this application.");
            }

            // Make sure all matches have the proper properties
            foreach (Match match in match_directory.Values)
            {
                if (match.Grade1 == 0) match.Grade1 = student_directory[match.Id1].Grade;
                if (!IsPassMatch(match) && match.Grade2 == 0) match.Grade2 = student_directory[match.Id2].Grade;

                changed = true;
            }

            // Make sure that the FormKey exists.
            if (!misc.ContainsKey(FormKey))
            {
                misc.Add(FormKey, "");
                changed = true;
            }

            if (changed) Save();
        }

        public int GetNumStudentsStillIn()
        {
            return GetAllParticipatingStudents().Count();
        }

        private void CreateStudentDirs()
        {
            // Create necessary dictionaries
            studentName_directory = new Hashtable();
            homeroom_directory = new Dictionary<int, List<Student>>();

            // Fill them.
            foreach (Student student in student_directory.Values)
            {
                // Start with the name
                string name = GetStudentNameKey(student);
                try
                {
                    studentName_directory.Add(name, student);
                }
                // In case there is someone with the same name
                catch (ArgumentException)
                {
                    // If a list already exists, add to it.
                    if (studentName_directory[name] is List<Student>) ((List<Student>)
                            studentName_directory[name]).Add(student);

                    // Otherwise, make one with both students.
                    else
                    {
                        Student studentThereAlready = (Student)studentName_directory[name];
                        studentName_directory[name] = new List<Student>
                        {
                            {studentThereAlready },
                            {student }
                        };
                    }
                }

                // Then the homerooms

                // If the homeroom list doesn't exist, add it.
                if (!homeroom_directory.ContainsKey(student.Homeroom))
                    homeroom_directory.Add(student.Homeroom, new List<Student>());

                // Add the student to the right homeroom
                homeroom_directory[student.Homeroom].Add(student);
            }
        }

        /// <summary>
        /// Tells you if a given match complies with the given filters.
        /// </summary>
        /// <param name="match">The match to test the filters against.</param>
        /// <param name="filter">The filter</param>
        /// <returns>True when the match complies, false otherwise.</returns>
        private bool CompliesWithFilters(Match match, Filter filter)
        {
            return ((match.Closed && filter.Closed) || !match.Closed && filter.Open)
                && filter.SelectedRounds.Contains(match.Round);
        }

        /// <summary>
        /// Fired when the name of the student that was searched for was found using search.
        /// </summary>
        public event EventHandler<string> StudentNameFoundThroughSearch;

        /// <summary>
        /// Command to give a list of matches relevant to the search
        /// </summary>
        /// <param name="query">SearchQuery object with search parameters.</param>
        /// <returns>List of matches relevant to the search</returns>
        public List<Match> GetMatches(SearchQuery query, Filter filter)
        {
            List<Match> resultsList = new List<Match>();

            // If match id is provided, get that match's info if it exists, else error.
            if (!string.IsNullOrWhiteSpace(query.MatchId))
            {
                if (match_directory.ContainsKey(query.MatchId)
                    && CompliesWithFilters(match_directory[query.MatchId], filter))
                {
                    resultsList.
                        Add(match_directory[query.MatchId].Clone());
                }

                else return null;
            }

            // If homeroom provided, get the students in that homeroom, else error.
            else if (query.Homeroom != 0)
            {
                if (!homeroom_directory.ContainsKey(query.Homeroom)) return null;
                List<Student> homeroomList = homeroom_directory[query.Homeroom];

                // Make nonexistent matches for each student to be displayed.
                foreach (Student student in homeroomList)
                {
                    resultsList.AddRange(GetMatchesFromStudentWithFilters(student, filter));
                }
            }

            // A student id was provided. Return null when it cannot be found
            else if (!string.IsNullOrEmpty(query.StudentNo))
            {
                if (!student_directory.ContainsKey(query.StudentNo)) resultsList = null;
                else
                {
                    Student student = student_directory[query.StudentNo];

                    if (student.MatchesParticipated.Count == 0 || student.MatchesParticipated[0] == null)
                    {
                        resultsList.Add(CreateFakeMatch(student));
                    }

                    resultsList.AddRange(GetMatchesFromStudentWithFilters(student, filter));

                    // Call the event.
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        StudentNameFoundThroughSearch?.Invoke(this, student.FullName);
                    }); 
                }
            }

            // The only remaining possibility is that a name was inputted. Find it, else error.
            if (!string.IsNullOrEmpty(query.StudentName))
            {
                if (resultsList == null) resultsList = new List<Match>();
                List<string> relevantKeys = new List<string>();

                // Find all substrings that match the inputted name.
                foreach (string key in studentName_directory.Keys)
                {
                    if (key.Contains(query.StudentName)) relevantKeys.Add(key);
                }

                if (relevantKeys.Count > 0)
                {
                    foreach (string key in relevantKeys)
                    {
                        // If there are more than one students at that key, add them all
                        if (studentName_directory[key] is List<Student>)
                            foreach (Student student in (List<Student>)studentName_directory[key])
                                resultsList.AddRange(GetMatchesFromStudentWithFilters(student, filter));

                        // If it's just one student, add them.
                        else resultsList.AddRange(GetMatchesFromStudentWithFilters((Student)studentName_directory[key], filter));
                    } 
                }

            }

            return resultsList;
        }

        /// <summary>
        /// Function to create a fake match for the given student.
        /// Fake matches are used when we want to display a single student's info on the mainDisplay
        /// </summary>
        /// <param name="student">The student data to create a fake match with</param>
        /// <returns>A fake match for the given student.</returns>
        public Match CreateFakeMatch(Student student)
        {
            return new Match
            {
                Id1 = student.Id,
                First1 = student.First,
                Last1 = student.Last,
                Grade1 = student.Grade,
                MatchId = "",
                Round = student.LastRoundParticipated,
                Id2 = string.Empty,
            };
        }

        /// <summary>
        /// Gets a list of matches  that the given student has participated in that comply
        /// with the filters.
        /// </summary>
        /// <param name="student">The student whose matches to extract.</param>
        /// <param name="filters">The filters to ensure the matches comply with.</param>
        /// <returns>List of the proper matches.</returns>
        private List<Match> GetMatchesFromStudentWithFilters(Student student, Filter filters)
        {
            List<Match> matches = new List<Match>();

            if (student.MatchesParticipated.Count == 0)
            {
                matches.Add(CreateFakeMatch(student));
                return matches;
            }

            foreach (string matchId in student.MatchesParticipated)
            {
                Match match = match_directory[matchId];
                if (CompliesWithFilters(match, filters)) matches.Add(match);
            }

            return matches;
        }

        /// <summary>
        /// Returns true if students exist already, false otherwise.
        /// </summary>
        /// <returns>True if at least one student exists, false otherwise. </returns>
        public bool IsData()
        {
            // True when there is discovered data.
            bool isData = false;
            Dictionary<int, List<Student>> grades = GetStudentsByGrade();

            foreach (KeyValuePair<int, List<Student>> pair in grades)
            {
                List<Student> grade = pair.Value;
                if (grade.Count() > 0)
                {
                    isData = true;
                    break;
                }
            }

            return isData;
        }

        /// <summary>
        /// Returns the match dictionary
        /// </summary>
        /// <returns>Match dictionary in the form of {"matchID", match}</returns>
        public Dictionary<string, Match> GetMatchDic() => match_directory;

        /// <summary>
        /// Returns a copy of the contents of match directory as a list
        /// </summary>
        /// <returns>A list of all matches</returns>
        public List<Match> GetMatchList()
        {
            List<Match> matchList = new List<Match>(match_directory.Values);
            List<Match> newMatchList = new List<Match>();
            foreach (Match match in matchList) newMatchList.Add(match);

            return newMatchList;
        }

        /// <summary>
        /// Add results to matches and calculate the new matches left and students still in the hunt.
        /// </summary>
        /// <param name="matchResults">The match results to be processed, in the form of MatchGuiResult objects.</param>
        public void AddMatchResults(List<PassingStudent> matchResults)
        {
            List<Match> affectedMatches = new List<Match>();
            // Update match and student data
            foreach (PassingStudent matchResult in matchResults)
            {
                Match match = matchResult.AffectedMatch;
                affectedMatches.Add(match);

                // Seems not needed, but in case two people are passed then it's needed.
                matchResult.AffectedStudent.In = true;

                // If the victor is student 1, mark student 2 as not in and pass student 1
                if (match.IsStudent1(matchResult.AffectedStudent))
                {
                    // If this match has already been closed, then the other student must have been passed too.
                    if (!match.Closed) student_directory[match.Id2].In = false;
                    match.Pass1 = true;
                }
                // Otherwise, mark student 1 as out and pass student 2
                else
                {
                    // If this match has already been closed, then the other student must have been passed too
                    if (!match.Closed) student_directory[match.Id1].In = false;
                    match.Pass2 = true;
                }

                match.Closed = true;
            }

            Save();

            MatchChangeEvent(this, affectedMatches.ToArray());
        }

        /// <summary>
        /// Function for adding match results from the imported csv file.
        /// </summary>
        /// <param name="resultsStudents">List of ResultStudents containing the results.</param>
        public void AddMatchResults(List<ResultStudent> resultsStudents)
        {
            // List of result students with supplied id
            List<string> idStudents = new List<string>();
            

            // True as soon as there is a problem.
            bool error = false;

            MatchResultImportLogger logger =
                new MatchResultImportLogger(DataFile, GetCurrRoundNo());

            // Start with looking for the student numbers, since that's faster
            foreach (ResultStudent student in resultsStudents)
            {
                // 0 is the null value for result student ids.
                if (string.IsNullOrWhiteSpace(student.Id))
                {
                    student.Id = GetStudentId(student.First, student.Last, student.Homeroom);

                    // If couldn't find student, id would be empty string.
                    if (string.IsNullOrEmpty(student.Id))
                    {
                        logger.AddLine("Could not find student with name "
                            + student.First + " " + student.Last);
                        error = true;
                    }
                }

                // Add the now known id to the list
                if (!string.IsNullOrWhiteSpace(student.Id)) idStudents.Add(student.Id);
            }

            // Validate all the student ids now that we have them
            foreach (string stuNo in idStudents)
            {
                if (!student_directory.ContainsKey(stuNo))
                {
                    logger.AddLine("Student with number " +
                        stuNo.ToString() + " does not exist.");
                }
                else if (student_directory[stuNo].In == false)
                {
                    logger.AddLine("Student with number " +
                        stuNo.ToString() + " is already out of the hunt.");
                }
            }

            List<Match> affectedMatches = new List<Match>(); // Matches affected by this function.

            if (error)
            {
                logger.SaveAndClose();
                MessageBox.Show("Errors importing match results. See log file",
                    "Error - Nothing imported", MessageBoxButton.OK, MessageBoxImage.Error);

                // Open up the file explorer to the log.
                Process.Start(logger.LogLocation);
            }
            else
            {
                // Now that all data is valid, proceed with closing of matches.
                Dictionary<string, Match> relevantMatches = GetOpenMatchesWithStudentIds(idStudents);

                // Update match and student data
                foreach (KeyValuePair<string, Match> keyValue in relevantMatches)
                {
                    Match match = match_directory[keyValue.Value.MatchId];

                    // Seems not needed, but in case two people are passed then it's needed.
                    student_directory[keyValue.Key].In = true;

                    // if the victor is student 1, mark student 2 as not in and pass student 1
                    if (keyValue.Key == match.Id1)
                    {
                        // If this match has already been closed, then the other student must have been passed too.
                        if (!match.Closed) student_directory[match.Id2].In = false;
                        match.Pass1 = true;
                    }
                    // Otherwise, mark student 1 as out and pass student 2
                    else
                    {
                        // If this match has already been closed, then the other student must have been passed too
                        if (!match.Closed) student_directory[match.Id1].In = false;
                        match.Pass2 = true;
                    }

                    match.Closed = true;
                    affectedMatches.Add(match);
                }

                Save();
                MatchChangeEvent?.Invoke(this, affectedMatches.ToArray());
            }
        }

        private void ResolveMatches()
        {
            // TODO fill in
        }

        /// <summary>
        /// Function for reopening the given match. Make sure that the match isn't a pass match!
        /// </summary>
        /// <param name="matchId">The id of the match to reopen.</param>
        public void ReopenMatch(string matchId)
        {
            // Reset required match and student parameters
            Match match = match_directory[matchId];
            match.Closed = false;
            match.Pass1 = false;
            match.Pass2 = false;

            // Bring the students back in.
            student_directory[match.Id1].In = true;
            student_directory[match.Id2].In = true;

            // Save and call match change event.
            Save();
            MatchChangeEvent?.Invoke(this, new Match[] { match } );
        }

        /// <summary>
        /// Function to close the given match and eliminate both students in it.
        /// </summary>
        /// <param name="matchId">The id of the match to close.</param>
        /// <param name="resolution">The manner in which the match is to be closed.</param>
        public void CloseMatch(string matchId)
        {
            // Get the match object, and make sure it has the right values
            Match match = match_directory[matchId];
            // If the match is closed already, don't touch it.
            if (match.Closed) return;
            match.Closed = true;
            match.Pass1 = false;
            match.Pass2 = false;

            // Put both students out.
            student_directory[match.Id1].In = false;
            if (!IsPassMatch(match)) student_directory[match.Id2].In = false;
        }

        /// <summary>
        /// Closes all the given matches asynchronously.
        /// </summary>
        /// <param name="matchesToClose">The list of matches to close</param>
        public async Task CloseMatchesAsync(List<Match> matchesToClose)
        {
            List<Task> tasks = new List<Task>();
            foreach (Match match in matchesToClose)
            {
                tasks.Add(Task.Run(() => CloseMatch(match.MatchId)));
            }

            // Wait for the processes to complete.
            await Task.WhenAll(tasks);
            // Save since close match won't do that anymore.
            Save();
            Application.Current.Dispatcher.Invoke(() =>
            {
                MatchChangeEvent?.Invoke(this, matchesToClose.ToArray());
            });
        }

        /// <summary>
        /// Closes all open matches, eliminating the students participating in them.
        /// </summary>
        public async Task CloseAllMatchesAsync()
        {
            List<Match> matchesToClose = GetOpenMatchesList();
            Task[] tasks = new Task[matchesToClose.Count];
            for (int i = 0; i < matchesToClose.Count; i++)
            {
                Match match = matchesToClose[i];
                tasks[i] = Task.Run(() => CloseMatch(match.MatchId));
            }
            // Save, since CloseMatch won't do it for us now.
            Save();
            await Task.WhenAll(tasks);
            Application.Current.Dispatcher.Invoke(() =>
            {
                MatchChangeEvent?.Invoke(this, matchesToClose.ToArray());
            });
        }

        /// <summary>
        /// Gets a list of cloned open matches in which a given student id is contained within.
        /// </summary>
        /// <param name="studentIds"></param>
        /// <returns></returns>
        private Dictionary<string, Match> GetOpenMatchesWithStudentIds(List<string> studentIds)
        {
            List<Match> openMatches = GetOpenMatchesList();
            Dictionary<string, Match> relevantMatches = new Dictionary<string, Match>();
            foreach (Match match in openMatches)
            {
                // If student 1 passes, add him/her
                if (studentIds.Contains(match.Id1))
                {
                    relevantMatches.Add(match.Id1, match);
                }

                /* If contains student 2 id, match is relevant
                 * You'll notice it's not an else if. This is because
                 * We might want to ask for both student ids for the match.
                 */
                if (studentIds.Contains(match.Id2))
                {
                    relevantMatches.Add(match.Id2, match);
                }
            }

            return relevantMatches;
        }

        /// <summary>
        /// Gives a copy of the match list containing only matches matching the filter.
        /// </summary>
        /// <param name="filter">The filter object to use to filter through the matches</param>
        /// <returns>A lit of the matches that meet the filter's criteria.</returns>
        public List<Match> GetMatches(Filter filter)
        {
            List<Match> matchList = GetMatchList();
            List<Match> returnList = new List<Match>();

            foreach (Match match in matchList)
            {
                if (((match.Closed && filter.Closed) || (!match.Closed && filter.Open))
                    && filter.SelectedRounds.Contains(match.Round))
                {
                    returnList.Add(match);
                }
            }

            return returnList;
        }

        public Student GetStudent(string id)
        {
            if (student_directory.ContainsKey(id)) return student_directory[id].Clone();
            else return null;
        }

        public Match GetMatch(string id)
        {
            return match_directory[id].Clone();
        }

        private string GetStudentId(string first, string last, int homeroom)
        {
            var nameEntry = studentName_directory[first + " " + last];

            // If a student with that name doesn't exist.
            if (nameEntry == null) return string.Empty;
            // If there is only one entry for that name
            else if (!(nameEntry is List<Student>)) return ((Student)nameEntry).Id;

            // If there are more students with that name
            else
            {
                foreach (Student student in (List<Student>)nameEntry)
                {
                    if (student.Homeroom == homeroom) return student.Id;
                }

                // In case we can't find one with those specifications
                return string.Empty;
            }
        }

        /// <summary>
        /// Returns a copy of the list of all currently open matches
        /// The Match objects have been cloned.
        /// </summary>
        /// <returns>A list of open matches as MatchDisplay Classes</returns>
        public List<Match> GetOpenMatchesList()
        {
            // Get a clone of the list of matches
            List<Match> matchList = GetMatchList();

            // Make new list for the open ones.
            List<Match> opnMatchList = new List<Match>();

            foreach (Match match in matchList)
            {
                if (!match.Closed) opnMatchList.Add(match);
            }
            return opnMatchList;
        }

        /// <summary>
        /// Get a list of the matches to be instantprinted.
        /// </summary>
        /// <returns>List of matches of the currrent round.</returns>
        public List<Match> GetInstantPrintMatches()
        {
            // Get a clone of the list of matches
            List<Match> matchList = GetMatchList();
            long currRound = GetCurrRoundNo();

            // Make new list for the open ones.
            List<Match> roundMatchList = new List<Match>();

            roundMatchList = matchList
                .Where(match => match.Round == currRound)
                .ToList();

            return roundMatchList;
        }

        /// <summary>
        /// Returns a copy of the list of all currently closed matches
        /// The Match objects have been cloned.
        /// </summary>
        /// <returns>A list of open matches as MatchDisplay Classes</returns>
        public List<Match> GetClosedMatchesList()
        {
            // Get a clone of the list of matches
            List<Match> matchList = GetMatchList();

            // Make new list for the open ones.
            List<Match> closedMatchList = new List<Match>();

            foreach (Match match in matchList)
            {
                if (match.Closed) closedMatchList.Add(match);
            }
            return closedMatchList;
        }

        /// <summary>
        /// Used to find the current match number being used to automatically
        /// create match ids.
        /// </summary>
        /// <returns>The current match number.</returns>
        public long CurrMatchNo
        {
            get
            {
                return (long)misc["TopMatch"];
            }
            set
            {
                misc["TopMatch"] = value;
                Save();
            }
        }

        /// <summary>
        /// Used to find the current round number. 
        /// </summary>
        /// <returns>The current round number.</returns>
        public long GetCurrRoundNo()
        {
            return (long)misc["RoundNo"];
        }

        /// <summary>
        /// Function to set the new round number to one above the current one.
        /// </summary>
        public void IncreaseCurrRoundNo()
        {
            misc["RoundNo"] = (long)misc["RoundNo"] + 1;
            long round = GetCurrRoundNo();

            foreach (KeyValuePair<string, Student> studentKeyValue in student_directory)
            {
                Student student = studentKeyValue.Value;
                if (student.In)
                {
                    student.LastRoundParticipated = round;
                }
            }

            Save();

            // Call round increased event
            RoundIncreased(this, new EventArgs());
        }

        /// <summary>
        /// Returns a dictionary of grades containing all students, in or out.
        /// </summary>
        /// <returns>A dictionary containing the student objects.
        /// Format: {key: grade, value: list of students in that grade}</returns>
        public Dictionary<int, List<Student>> GetAllStudentsByGrade()
        {
            Dictionary<int, List<Student>> studentDic = new Dictionary<int, List<Student>>();

            foreach (KeyValuePair<string, Student> studentKeyValue in student_directory)
            {
                // If the grade doesn't exist in the dictionary, add it.
                if (!studentDic.ContainsKey(studentKeyValue.Value.Grade)) studentDic.Add(studentKeyValue.Value.Grade, new List<Student>());

                // Add the student to their appropriate grade.
                studentDic[studentKeyValue.Value.Grade].Add(studentKeyValue.Value);
            }

            return studentDic;
        }

        /// <summary>
        /// Returns the dictionary of grades containing all in students.
        /// </summary>
        /// <returns>A dictionary containing the student objects.
        /// Format: {key: grade, value: list of students in that grade}</returns>
        public Dictionary<int, List<Student>> GetStudentsByGrade()
        {
            Dictionary<int, List<Student>> studentDic = new Dictionary<int, List<Student>>();
            foreach (Student student in student_directory.Values)
            {
                ;
                if (student.In)
                {
                    // If that grade has not been put in the student dictionary, add the grade.
                    if (!studentDic.ContainsKey(student.Grade))
                        studentDic.Add(student.Grade, new List<Student>());

                    // Add the student to their proper grade.
                    studentDic[student.Grade].Add(student);
                }

            }

            return studentDic;
        }

        /// <summary>
        /// Returns a list of all students who are still in the hunt
        /// </summary>
        /// <returns></returns>
        public List<Student> GetAllParticipatingStudents()
        {
            List<Student> inStudentsList = new List<Student>();

            foreach (Student student in student_directory.Values)
            {
                if (student.In) inStudentsList.Add(student);
            }

            return inStudentsList;
        }

        /// <summary> 
        /// Adds students to the master student dictionary
        /// </summary>
        /// <param name="students">The list of students to add.</param>
        public bool AddStudents(Student[] students)
        {
            // So that we can rollback any changes.
            Dictionary<string, Student> safeStudent_directory = new Dictionary<string, Student>(student_directory);
            Hashtable safeStudentName_directory = new Hashtable(studentName_directory);
            Dictionary<int, List<Student>> safeHomeroom_directory = new Dictionary<int, List<Student>>(homeroom_directory);

            // Used in case of an error message to communicate which student ID exists already
            string id = string.Empty;
            try
            {
                // Add the students to the student list
                foreach (Student student in students)
                {
                    // Add to the student id dictionary
                    id = student.Id;
                    safeStudent_directory.Add(student.Id, student);

                    // Also add the student to the studentNameDirectory
                    // Start with the name
                    string name = student.First.ToUpper() + " " + student.Last.ToUpper();
                    try
                    {
                        safeStudentName_directory.Add(name, student);
                    }
                    // In case there is someone with the same name
                    catch (ArgumentException)
                    {
                        // If a list already exists, add to it.
                        if (studentName_directory[name] is List<Student>) ((List<Student>)
                                safeStudentName_directory[name]).Add(student);

                        // Otherwise, make one with both students.
                        else
                        {
                            Student studentThereAlready = (Student)studentName_directory[name];
                            studentName_directory[name] = new List<Student>
                            {
                                {studentThereAlready },
                                {student }
                            };
                        }
                    }

                    // Add the student to the homeroom directory
                    // If homeroom exists, easy adding
                    if (safeHomeroom_directory.ContainsKey(student.Homeroom))
                        safeHomeroom_directory[student.Homeroom].Add(student);

                    // Else, create homeroom for them
                    else
                    {
                        List<Student> hmrmList = new List<Student>
                        {
                            {student }
                        };
                        safeHomeroom_directory.Add(student.Homeroom, hmrmList);
                    }
                }
            }
            catch (ArgumentException)
            {
                MessageBox.Show("A student with ID " + id.ToString() +
                    " already exists, or two students with that id were just imported.",
                    "Duplicate Student ID", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            ReplaceOldStuDicWithNewOne(safeStudent_directory, safeHomeroom_directory, safeStudentName_directory);
            Save();

            // Invoke students added event
            Application.Current.Dispatcher.Invoke(() =>
            {
                StudentsImported?.Invoke(this, new EventArgs());
            });

            return true;
        }

        /// <summary>
        /// Used just after creating a backup of the student dictionary to push the
        /// backup onto the master.
        /// </summary>
        /// <param name="newStudentDic">The backup that was made.</param>
        private void ReplaceOldStuDicWithNewOne(Dictionary<string, Student> newStudentDic,
            Dictionary<int, List<Student>> newStudentHmrmDic, Hashtable newStudentNameDic)
        {
            data[STUDENTKEY] = new Dictionary<string, Student>(newStudentDic);
            student_directory = (Dictionary<string, Student>)data[STUDENTKEY];
            studentName_directory = newStudentNameDic;
            homeroom_directory = newStudentHmrmDic;
        }

        /// <summary>
        /// Function to add created matches and save them.
        /// </summary>
        /// <param name="matchesToAdd">List of matches to add to the match dictionary</param>
        /// <param name="multiThread">Set to true when accessing this from a thread other
        /// than the main one.</param>
        /// <param name="increaseRoundNo">Whether or not to increase the current round number as well. Defaults to true, set false to not.</param>
        public void AddMatches(List<Match> matchesToAdd, bool multiThread = false, bool increaseRoundNo = true)
        {
            // Update the match numbers.
            CurrMatchNo = matchesToAdd[matchesToAdd.Count - 1].MatchNumber;

            foreach (Match match in matchesToAdd)
            {
                // Add the matches onto the match dictionary
                match_directory.Add(match.MatchId, match);

                // Update the first student object
                student_directory[match.Id1].CurrMatchID = match.MatchId;

                /* Because there will be no student with id = 0, this is the id of a "pass" student
                 * We don't want to try to find a nonexistent "pass" student, so do this */
                if (!string.IsNullOrEmpty(match.Id2))
                {
                    student_directory[match.Id2].CurrMatchID = match.MatchId;
                }
                /* Otherwise, the first student of the match 
                 * is being passed and their HasBeenPassed property should be set true. */
                else
                {
                    student_directory[match.Id1].HasBeenPassed = true;
                }
            }

            /* If everyone has been passed at least once, 
             * we need to reset the property so they can be passed again. */
            if (HasEveryOneBeenPassedOnce()) ResetPassers();

            if (!multiThread) Save();


            // Since this only happens once per round, also increase the round number
            if (increaseRoundNo) IncreaseCurrRoundNo();
        }

        /// <summary>
        /// Adds the given edited matches to the match list, and saves.
        /// </summary>
        /// <param name="matchesToAdd"></param>
        public async Task AddEditedMatches(List<Match> matchesToAdd)
        {
            await Task.Run(() => AddMatches(matchesToAdd, true, false));

            // Loop around and make sure that all the students that should be in are in.
            foreach (Match match in matchesToAdd)
            {
                // Make sure the students are in, and close their matches.
                if (student_directory.ContainsKey(match.Id1))
                {
                    Student student1 = student_directory[match.Id1];
                    student1.In = true;

                    match_directory[student1.MatchesParticipated[student1.MatchesParticipated.Count - 2]].Closed = true;
                }

                if (student_directory.ContainsKey(match.Id2))
                {
                    Student student2 = student_directory[match.Id2];
                    student2.In = true;

                    match_directory[student2.MatchesParticipated[student2.MatchesParticipated.Count - 2]].Closed = true;
                }
            }

            Save();

            // Just call the event for multi-thread
            MatchChangeEvent.Invoke(this, matchesToAdd.ToArray());
        }

        /// <summary>
        /// Eliminates the given students and closes their matches.
        /// </summary>
        /// <param name="studentsToEliminate">The students to eliminate.</param>
        public async Task EliminateStudents(List<Student> studentsToEliminate)
        {
            Task[] tasks = new Task[studentsToEliminate.Count];  // TODO make sure this runs async.
            for (int i = 0; i < studentsToEliminate.Count; i++)
            {

                Student student = studentsToEliminate.ElementAt(i);
                tasks[i] = Task.Run(() => EliminateStudent(student));
            }

            await Task.WhenAll(tasks);
            Save();
        }

        /// <summary>
        /// Eliminates the provided students from the hunt.
        /// </summary>
        /// <param name="studentToEliminate">The students to eliminate.</param>
        public void EliminateStudent(Student studentToEliminate)
        {
            // Make sure the student exists.
            if (!student_directory.ContainsKey(studentToEliminate.Id)) return;


            Student student = student_directory[studentToEliminate.Id];

            // Eliminate the student.
            student.In = false;

            // Close the match
            match_directory[student.CurrMatchID].Closed = true;
        }

        /// <summary>
        /// Determines if all students have been passed through a round once, and if so returns true.
        /// This is important because in small reindeer hunts, it is possible for 
        /// every student to be passed once and once this happens we want to 
        /// reset it so that we can begin to pass them once again.
        /// </summary>
        /// <returns>True if all student's HasBeenPasses property is true, false otherwise.</returns>
        private bool HasEveryOneBeenPassedOnce()
        {
            Dictionary<int, List<Student>> studentsByGrade = GetAllStudentsByGrade();
            foreach (List<Student> gradeList in studentsByGrade.Values)
            {
                foreach (Student student in gradeList)
                {
                    /* Since it is possible for one odd student in every grade,
                     * we make sure there is no grade with less than one passable
                     * student. 
                     */
                    if (!student.HasBeenPassed) return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Function that resets the HasBeenPassed values of all students.
        /// </summary>
        private void ResetPassers()
        {
            foreach (Student student in student_directory.Values)
            {
                student.HasBeenPassed = false;
            }
        }

        /// <summary>
        /// Saves all students added and settings changed.
        /// </summary>
        /// <param name="overrider">If you want to override the conditions that block save, set
        /// this to true.</param>
        public void Save(bool overrider = false)
        {
            if (!IsFFARound || overrider) DataFile.Write(data);
        }

        /// <summary>
        /// Returns a boolean declaring whether or not a given match is open
        /// </summary>
        /// <param name="match">The match object that you want to verify is open.</param>
        /// <returns>Boolean, true when match is open, false otherwise.</returns>
        public bool MatchIsOpen(Match match)
        {
            return !match.Closed;
        }

        /// <summary>
        /// Returns true when the next round should be the free for all round (FFA)
        /// </summary>
        public bool IsTimeForFFA
        {
            get
            {
                // Insert logic for determining this
                int studentsLeft = GetAllParticipatingStudents().Count();
                if (studentsLeft <= 16) return true;
                return false;
            }
        }

        /// <summary>
        /// Used to set up the file for first-time use. 
        /// </summary>
        protected void FirstTimeSetup()
        {
            // Create the students dictionary
            Dictionary<string, Student> student_Dic = new Dictionary<string, Student>();

            // Create new matches list
            Dictionary<string, Match> matches = new Dictionary<string, Match>();

            // Create the hasthtable that will store various other values
            Hashtable various_data = new Hashtable
            {
                // The current match number. Used for creating unique match ids
                { TopMatchKey, 0 },

                // The current round number. Starts at 0 to indicate no round is in progress.
                { RoundNoKey, 0 },

                // Boolean representing if we're in the FFA round or not
                { IsFFAKey, false },

                // String representing the current round's end date
                { EndDateKey, "" },

                // String URL of the form
                {FormKey, "" },

                // The current version number of the application
                {VersionKey, StartupWindow.APPLICATION_VERSION }
            };

            Dictionary<int, Victor> victorList = new Dictionary<int, Victor>();

            // Create the data dictionary
            Hashtable data = new Hashtable
            {
                { DataFileIO.StudentDataLoc, student_Dic },
                { DataFileIO.MatchDataLoc, matches },
                { DataFileIO.MiscDataLoc, various_data }
            };

            DataFile.Write(data);
        }

        /// <summary>
        /// Returns a dictionary of grades which contains a dictionary of the 
        /// students in that grade by homeroom.
        /// </summary>
        /// <returns>A dictionary of grades which contains a dictionary of the 
        /// students in that grade by homeroom.</returns>
        public Dictionary<int, Dictionary<int, List<Student>>> GetStudentsByGradeAndHomeroom()
        {

            // Step 1: Sort all students into one big gradeless homeroom list. Make sure students are clones.


            Dictionary<int, List<Student>> homeroomList = new Dictionary<int, List<Student>>();

            // Duplicate all the students in the homeroom directory so they're not modified externally.
            foreach (KeyValuePair<int, List<Student>> studentKV in homeroom_directory)
            {
                List<Student> homeroom = new List<Student>();

                for (int a = 0; a < studentKV.Value.Count; a++)
                {
                    homeroom.Add(studentKV.Value[a]);
                }

                // Add it to the list
                homeroomList.Add(studentKV.Key, homeroom);
            }

            /* 
             * Step 2: Take that homeroom list and sort the homerooms into a grades dictionary.
             * 
             * For students from a different grade than the homeroom is for, make an entry for
             * that homeroom in those students' grades and put them in it. In this manner. 
             * 
             * For example, say you have homeroom 1005 with Gr9Student1, Gr9Student2, Gr10Student1, Gr10Student2, Gr10Student3.
             * Two lists would be made for the homeroom: {1005 : [Gr9Student1, Gr9Student2]} and {1005, [Gr10Student1, Gr10Student2, Gr10Student3]}
             * The first list would go in the grade 9's dictionary entry while the second would go in the grade 10's. 
             */

            Dictionary<int, Dictionary<int, List<Student>>> gradeHomeroomList = new Dictionary<int, Dictionary<int, List<Student>>>();

            foreach (KeyValuePair<int, List<Student>> hmrm_students in homeroomList)
            {

                /* 
                 * Seperate the students into different homeroom 
                 * lists of the same homeroom by grade
                 */
                List<List<Student>> sorted = hmrm_students.Value
                     .GroupBy(student => student.Grade)
                     .Select(grp => grp.ToList())
                     .ToList();

                // Make it into dictionary and add to master list
                foreach (List<Student> student_list in sorted)
                {
                    // Get the grade by checking the grade of the first student.
                    int grade = student_list[0].Grade;

                    if (!gradeHomeroomList.ContainsKey(grade)) gradeHomeroomList.Add(grade, new Dictionary<int, List<Student>>());
                    gradeHomeroomList[grade].Add(hmrm_students.Key, student_list);
                }

            }

            return gradeHomeroomList;
        }

        /// <summary>
        /// Function to return the dataFile object.
        /// </summary>
        /// <returns>The dataFile object</returns>
        public DataFileIO GetDataFile()
        {
            return DataFile;
        }

        public bool IsFFARound
        {
            get
            {
                return (bool)(misc["IsFFA"]);
            }

            set
            {
                misc["IsFFA"] = value;
                Save(true);
            }
        }

        /// <summary>
        /// Determines whether the given match is a pass match
        /// </summary>
        /// <param name="match">The match to check.</param>
        /// <returns>True if it is a pass match, false otherwise.</returns>
        public static bool IsPassMatch(Match match)
        {
            return (string.IsNullOrEmpty(match.Id2) && match.First2 == "Pass" && match.Last2 == "Pass" && match.Home2 == 0);
        }

        /// <summary>
        /// Determines whether the given match is valid.
        /// </summary>
        /// <param name="match">The match to validate.</param>
        /// <returns>True if valid, false otherwise.</returns>
        public bool IsValidMatch(Match match)
        {
            return match_directory.ContainsKey(match.MatchId);
        }

        /// <summary>
        /// Function that properly returns the students involved in a match.
        /// This is used because it will properly skip passmatches
        /// </summary>
        /// <param name="match">The match from which to get the students</param>
        /// <returns>A list of the valid students involved in the match, if any.</returns>
        public List<Student> GetStudentsInMatch(Match match)
        {
            List<Student> returnable = new List<Student>();

            if (student_directory.ContainsKey(match.Id1)) returnable.Add(student_directory[match.Id1]);
            if (student_directory.ContainsKey(match.Id2)) returnable.Add(student_directory[match.Id2]);

            return returnable;
        }

        /// <summary>
        /// Function to delete the student specified by the student object or student id.
        /// </summary>
        /// <param name="studentId"></param>
        /// <param name="student"></param>
        public void DeleteStudent(string studentId = "", Student student = null)
        {
            string id;
            if (string.IsNullOrEmpty(studentId) && student == null)
            {
                throw new ArgumentException("StudentId and student parameters cannot be ignored");
            }
            else if (GetCurrRoundNo() != 0)
            {
                throw new ArgumentException("Cannot delete students past round 0.");
            }

            // If just the studentId is null, 
            else if (string.IsNullOrEmpty(studentId))
            {
                id = student.Id;
            }
            else
            {
                id = studentId;
            }

            Student studentToRemove = student_directory[id];

            // Remove the student from all lists and save
            student_directory.Remove(id);
            homeroom_directory[studentToRemove.Homeroom].Remove(studentToRemove);

            string studentName = string.Format("{0} {1}", studentToRemove.First.ToUpper(), studentToRemove.Last.ToUpper());
            var data = studentName_directory[studentName];

            if (data is List<Student>)
            {
                ((List<Student>)data).Remove(studentToRemove);
            }
            else
            {
                studentName_directory.Remove(studentName);
            }

            Save();

        }

        /// <summary>
        /// Function to update the properties and matches of the given student
        /// after changes are made to that student.
        /// </summary>
        /// <param name="updated_student">The updated copy of the student object.</param>
        public void UpdateStudent(Student updated_student)
        {
            /* Update the student, including the key of the student in the name
             * directory and position in the homeroom directory.
             */
            Student oldStudent = student_directory[updated_student.Id];

            // Update the student in the name directory
            studentName_directory.Remove(GetStudentNameKey(oldStudent));
            studentName_directory.Add(GetStudentNameKey(updated_student), updated_student);



            // Update the student in the homeroom directory
            // Remove
            homeroom_directory[oldStudent.Homeroom].Remove(oldStudent);
            // If the homeroom now has no one, remove it.
            if (homeroom_directory[oldStudent.Homeroom].Count == 0) homeroom_directory.Remove(oldStudent.Homeroom);

            // Add
            // If the homeroom doesn't exist in the homeroom directory, create it.
            if (!homeroom_directory.ContainsKey(updated_student.Homeroom)) homeroom_directory.Add(updated_student.Homeroom, new List<Student>());
            // Add the student to their homeroom
            homeroom_directory[updated_student.Homeroom].Add(updated_student);



            // Update the student in the master student directory
            student_directory[updated_student.Id] = updated_student;

            // Update the matches that they're in.
            List<Match> affectedMatches = new List<Match>();
            foreach (string matchId in updated_student.MatchesParticipated)
            {
                Match match = match_directory[matchId];

                // Figure out if they're student 1 or 2, and update accordingly.
                if (match.Id1 == updated_student.Id)
                {
                    match.First1 = updated_student.First;
                    match.Last1 = updated_student.Last;
                    match.Home1 = updated_student.Homeroom;
                    match.Grade1 = updated_student.Grade;
                }
                else
                {
                    match.First2 = updated_student.First;
                    match.Last2 = updated_student.Last;
                    match.Home2 = updated_student.Homeroom;
                    match.Grade2 = updated_student.Grade;
                }
                affectedMatches.Add(match);
            }

            // Call the matches changed event
            MatchChangeEvent?.Invoke(this, affectedMatches.ToArray());

            // Save changes
            Save();
        }

        /// <summary>
        /// The end date of the current round, in no particular format.
        /// It will be printed as is onto the licenses, no validation.
        /// </summary>
        public string RoundEndDate
        {
            get
            {
                return misc[EndDateKey].ToString();
            }
            set
            {
                misc[EndDateKey] = value;
                Save();
            }
        }

        /// <summary>
        /// Gets the key of the student in the name directory
        /// </summary>
        /// <param name="student">The student to get the key of</param>
        /// <returns>The string key of the student in the student name directory</returns>
        public static string GetStudentNameKey(Student student)
        {
            return string.Format("{0} {1}", student.First, student.Last).ToUpper();
        }
    }
}
