﻿using Reindeer_Hunter.Hunt;
using Reindeer_Hunter.Subsystems;
using Reindeer_Hunter.Subsystems.ProcessButtonCommands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Media;

namespace Reindeer_Hunter.ThreadMonitors
{
    public class MatchMakeHandler
    {
        /* Used in the lock blocks to make sure that only one thread accesses
         * Sensitive stuff at once */
        private readonly object Key = new object();

        private Queue<Message> comms;
        private School _School;
        private Thread matchMakeThread;
        private Matcher matcher;
        private ProcessButtonSubsystem subsystem;

        public MatchMakeHandler(School school, ProcessButtonSubsystem subsystemInCharge)
        {

            // First determine what type of matchmaking we'll be doing
            MatchmakeDialog selectionDialog = new MatchmakeDialog();
            selectionDialog.ShowDialog();

            MatchmakeDialog.MatchmakeStatus result = selectionDialog.GetResult();

            // Get the end date inputted by the user.
            string endDate = selectionDialog.EndDate;

            if (result == MatchmakeDialog.MatchmakeStatus.Cancelled) return;

            subsystem = subsystemInCharge;

            // Create the queue for communications purposes.
            comms = new Queue<Message>();

            // Instantiate school object for simplicity
            this._School = school;

            // Create the matchmaker and then assign the thread target to it
            // +1 to current round because we want next round's matches.
            if (result == MatchmakeDialog.MatchmakeStatus.Grades)
            {
                matcher = new Matcher(school.GetCurrRoundNo() + 1, school.CurrMatchNo, Key, endDate,
                    comms, studentsDic: school.GetStudentsByGrade());
            }
            else if (result == MatchmakeDialog.MatchmakeStatus.Students)
            {
                matcher = new Matcher(school.GetCurrRoundNo() + 1, school.CurrMatchNo, Key, endDate,
                    comms, studentList: school.GetAllParticipatingStudents());
            }
            else
            {
                matcher = new Matcher(school.GetCurrRoundNo() + 1, school.CurrMatchNo, Key, endDate,
                    comms, homeroomList: school.GetStudentsByGradeAndHomeroom());
            }

            matchMakeThread = new Thread(matcher.Generate)
            {
                Name = "Matchmaker",
                IsBackground = true
            };
            matchMakeThread.Start();

            // Put the execute function into the mainloop to be executed
            CompositionTarget.Rendering += MatchmakeMonitor;
        }

        public void MatchmakeMonitor(object sender, EventArgs e)
        {
            // Lock it so we don't get problems.
            lock (Key)
            {
                // Don't do anything if no new data has been sent. 
                if (comms.Count() <= 0) return;

                // Convert queue to list and retrieve last value
                List<Message> returnList = comms.ToList<Message>();
                Message returnValue = returnList[returnList.Count() - 1];

                // Clear queue
                comms.Clear();

                // Update the progress displayed in the GUI
                subsystem.UpdateOperationStatus(returnValue.MessageText, returnValue.ProgressDecimal);

                // Once the matches have been added, let's do stuff.
                if (returnValue.Matches != null)
                {
                    //  Terminate the thread
                    matchMakeThread.Join();

                    subsystem.GenerationInfo = new Tuple<long, List<Match>, string>
                        (returnValue.Matches[returnValue.Matches.Count - 1].MatchNumber, returnValue.Matches, returnValue.EndDate);


                    // Unsubscribe from the rendering event
                    CompositionTarget.Rendering -= MatchmakeMonitor;
                }
            }
        }
    }
}
