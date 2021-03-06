﻿using Reindeer_Hunter.Data_Classes;
using Reindeer_Hunter.Hunt;
using System.Collections.Generic;
using System.Diagnostics;

namespace Reindeer_Hunter
{
    public class ImportStudents
    {
        private School _School;
        private string[] FileLocation;
        private Queue<bool> Comms;

        public ImportStudents(string[] fileLocation, School school, Queue<bool> comms)
        {
            Comms = comms;
            FileLocation = fileLocation;
            _School = school;
        }

        /// <summary>
        /// The actual method that then imports the students.
        /// </summary>
        public void Import()
        {
            List<object[]> resultsList = CSVHandler.Import(CSVHandler.ImportType.Students, pathsList: FileLocation);
            List<Student> students_to_add = new List<Student>();

            // In case of problems.
            if (resultsList == null) return;

            long round =_School.GetCurrRoundNo();

            foreach (object[] result in resultsList)
            {
                // In case of any import errors.
                if (result == null)
                {
                    Finish(false);
                    return;
                }
                // Otherwise we can proceed.
                else
                {
                    foreach (RawStudent importedStudent in result)
                    {
                        string studentId = (string.IsNullOrEmpty(importedStudent.Id)) ? string.Format("Gen{0}", Stopwatch.GetTimestamp().ToString()) : importedStudent.Id;
                        // Make new student, set the student's round number and add them to the new list
                        Student student = new Student
                        {
                            First = importedStudent.First,
                            Last = importedStudent.Last,
                            Id = studentId,
                            Grade = importedStudent.Grade,
                            Homeroom = importedStudent.Homeroom,
                            LastRoundParticipated = round,
                            In = true,
                            MatchesParticipated = new List<string>()
                        };

                        students_to_add.Add(student);
                    } 
                }
            }

            // Add the students.
            bool importedProperly = _School.AddStudents(students_to_add.ToArray());

            Finish(importedProperly);
        }

        /// <summary>
        /// Function to properly send a message at the end of the thread
        /// </summary>
        /// <param name="finishedProperly"></param>
        private void Finish(bool finishedProperly)
        {
            // Finish with successful value
            Comms.Enqueue(finishedProperly);
        }
    }
}
