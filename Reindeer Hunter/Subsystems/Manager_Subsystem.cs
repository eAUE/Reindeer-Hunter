﻿using Microsoft.Win32;
using Reindeer_Hunter.Data_Classes;
using Reindeer_Hunter.Hunt;
using Reindeer_Hunter.Subsystems.Student_Manager;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Reindeer_Hunter.Subsystems
{
    /// <summary>
    /// Subsystem in charge of importing students and match results.
    /// </summary>
    public class Manager_Subsystem : Subsystem
    {
        public RelayCommand ImportStudents { get; } = new RelayCommand();
        public RelayCommand ImportMatchResults { get; } = new RelayCommand();
        public RelayCommand ExportStudents { get; } = new RelayCommand();
        public RelayCommand ImportPDF { get; } = new RelayCommand
        {
            CanExecuteDeterminer = () => true
        };
        public RelayCommand ExportMatchesCommand { get; } = new RelayCommand()
        {
            CanExecuteDeterminer = () => true
        };

        private StartupWindow MasterWindow;

        protected override void OnHomePageSet(object sender, EventArgs e)
        {
            base.OnHomePageSet(sender, e);

            #region Setting Up RelayCommands
            // Set functions for the Relay Commands
            ImportStudents.FunctionToExecute = Import_Students;
            ImportStudents.CanExecuteDeterminer = Can_Import_Students;

            ImportMatchResults.FunctionToExecute = Import_Results;
            ImportMatchResults.CanExecuteDeterminer = Can_Import_Results;

            ExportStudents.CanExecuteDeterminer = Can_Export_Students;
            ExportStudents.FunctionToExecute = Export_Students;

            ImportPDF.FunctionToExecute = Import_PDF;

            MasterWindow = Manager.Home.MasterWindow;

            // Exporting the current matches command
            ExportMatchesCommand.FunctionToExecute = (param) => Export_Current_Matches();
            #endregion

            #region Subscribing to events
            // Subscribe to events that will merit refresh
            school.MatchChangeEvent += (a, b) => Refresh();
            school.RoundIncreased += Refresh;
            Manager._ProcessButtonSubsystem.WentToFFA += Refresh;
            #endregion

            Refresh();
        }

        /// <summary>
        /// Determines if we can import students still
        /// </summary>
        /// <param name="parameter"></param>
        public bool Can_Import_Students()
        {
            // You can only import during round 0. or during Free For all
            bool result = (school != null && !school.IsFFARound);
            return result;
        }

        /// <summary>
        /// Determiner for if students can be exported
        /// </summary>
        /// <returns>True or false, true if student exporting should be allowed.</returns>
        public bool Can_Export_Students()
        {
            /* 
             * Can only export students if there is more than one on screen, since we need
             * to export the students currently on screen.
             */
            return (Manager._FiltersAndSearch.Number_Of_Matches_Being_Displayed > 0);
        }

        /// <summary>
        /// The function called by the ExportStudents command.
        /// </summary>
        /// <param name="parameter"></param>
        public async void Export_Students(object parameter)
        {
            // Get the matches whose students need exporting. 
            List<Match> studentsToExport = await Task.Run(() => Manager._FiltersAndSearch.GetMatches());

            if (studentsToExport.Count > 0)
            {
                SaveFileDialog saveDialog = new SaveFileDialog
                {

                    // Open the file dialog to the user's directory
                    InitialDirectory = DataFileIO.InitialDirectory,

                    // Filter only for comma-seperated value files. 
                    Filter = "csv files (*.csv)|*.csv",
                    FilterIndex = 2,
                    RestoreDirectory = true
                };

                saveDialog.ShowDialog();

                string path = saveDialog.FileName;

                /* 
                 * Check if the filepath isn't empty, which it won't if the user cancels.
                 * If it is empty, return.
                 */
                if (!string.IsNullOrEmpty(path))
                {
                    // Make the object that will handle all else.
                    new ExportStudents(studentsToExport, school, path);

                    // Set the last opened directory to this directory.
                    DataFileIO.LastOpenedDirectory = Path.GetDirectoryName(path);
                } 
            }
        }

        /// <summary>
        /// Exports the matches currently being viewed by the user.
        /// </summary>
        private void Export_Current_Matches()
        {
            List<Match> matchesToExport = Manager._FiltersAndSearch.MainDisplay_Display_List;

            // Only continue if there are actually matches to be exported.
            if (matchesToExport.Count > 0)
            {
                SaveFileDialog saveDialog = new SaveFileDialog
                {
                    // Open the file dialog to the correct directory.
                    InitialDirectory = DataFileIO.InitialDirectory,
                    Filter = "csv files (*.csv)|*.csv",
                    FilterIndex = 2,
                    RestoreDirectory = true
                };

                saveDialog.ShowDialog();
                string path = saveDialog.FileName;
                if (!string.IsNullOrEmpty(path))
                {
                    ExportMatches(matchesToExport, path);

                    DataFileIO.LastOpenedDirectory = Path.GetDirectoryName(path);
                }


            }
        }

        /// <summary>
        /// Exports the given matches to the given file path.
        /// </summary>
        /// <param name="matchesToExport">The matches to be exported.</param>
        /// <param name="path">The path of the CSV file to which the mathes should be saved.</param>
        public static async void ExportMatches(List<Match> matchesToExport, string path)
        {
            try
            {
                await ExportMatchesAsync(matchesToExport, path);
                // Once the stuff has been completed, notify the user.
                MessageBox.Show("Completed Operation.", "Completed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (IOException)
            {
                MessageBox.Show("Failed to access file.", "Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
           
        }

        /// <summary>
        /// Exports the given matches to the given file path asynchronously.
        /// </summary>
        /// <param name="matchesToExport">The matches to be exported.</param>
        /// <param name="path">The path of the CSV file to which the mathes should be saved.</param>
        public static async Task ExportMatchesAsync(List<Match> matchesToExport, string path)
        {
            await Task.Run(() => CSVHandler.ExportMatches(matchesToExport, path));
        }

        /// <summary>
        /// Function that the realay command calls to execute student import
        /// </summary>
        /// <param name="parameter"></param>
        public void Import_Students(object parameter)
        {
            MasterWindow.ImportStudents();
        }

        public bool Can_Import_Results()
        {
            // If there are no matches, there is no importing results for matches.
            if (school.NumOpenMatches <= 0) return false;
            else return true;
        }

        /// <summary>
        /// Function to import a new template pdf file. 
        /// </summary>
        /// <param name="parameter"></param>
        public void Import_PDF(object parameter)
        {
            // Just tell the data file to import it, and catch the error
            try
            {
                school.DataFile.Import_Template_PDF();
            }
            catch (IOException)
            {
                return;
            }
        }

        /// <summary>
        /// Function that the relat command calls to import results.
        /// </summary>
        /// <param name="parameter"></param>
        public void Import_Results(object parameter)
        {
            List<ResultStudent> results = new List<ResultStudent>();
            object[] inputtedResults;
            try
            {
                OpenFileDialog csvFileDialog = new OpenFileDialog
                {
                    InitialDirectory = DataFileIO.InitialDirectory,
                    Filter = "csv files (*.csv)|*.csv",
                    FilterIndex = 2,
                    RestoreDirectory = true,
                    Multiselect = false
                };

                csvFileDialog.ShowDialog();


                inputtedResults = CSVHandler.Import(CSVHandler.ImportType.MatchResults, 
                    filePath: csvFileDialog.FileName).ElementAt(0);

                DataFileIO.LastOpenedDirectory = Path.GetDirectoryName(csvFileDialog.FileName);
            }
            catch (ArgumentNullException)
            {
                return;
            }

            foreach (ResultStudent student in inputtedResults)
            {
                student.First = student.First.ToUpper().Trim();
                student.Last = student.Last.ToUpper().Trim();
                results.Add(student);
            }

            school.AddMatchResults(results);
        }

        /// <summary>
        /// Function that refreshes the commands when needed
        /// </summary>
        private void Refresh(object sender = null, EventArgs e = null)
        {
            ImportStudents.RaiseCanExecuteChanged();
            ImportMatchResults.RaiseCanExecuteChanged();
            ExportStudents.RaiseCanExecuteChanged();
        }
    }
}
