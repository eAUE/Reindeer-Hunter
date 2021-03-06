﻿using System;
using System.Windows.Input;
using Reindeer_Hunter.ThreadMonitors;

namespace Reindeer_Hunter.Subsystems.ProcessButtonCommands
{
    public class Process : ICommand
    {
        InstantPrintHandler Printer;

        // The subsystem in charge of this command
        public ProcessButtonSubsystem ProcessButtonSubsystem { get; set; }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
        {
            // We can always execute
            return true;
        }

        /// <summary>
        /// Function that begins either matchmaking or the 
        /// </summary>
        /// <param name="parameter"></param>
        public void Execute(object parameter)
        {
            ProcessButtonSubsystem.Status status = (ProcessButtonSubsystem.Status)parameter;

            // Determine what action to take using the status.

            // Matchmake
            if (status == ProcessButtonSubsystem.Status.MatchMaking)
            {
                // Create matchmaker class.
                MatchMakeHandler matcher = new MatchMakeHandler(
                    ProcessButtonSubsystem.ManagerProperty._School, 
                    ProcessButtonSubsystem);
            }

            // Go to the FFA page
            else if (status == ProcessButtonSubsystem.Status.FFA)
            {
                ProcessButtonSubsystem.GoToFFA();
            }

            // Instant Print
            else
            {
                // If we're currently printing, don't print twice.

                if (Printer != null && Printer.IsPrinting) return;

                 Printer = new InstantPrintHandler(
                    ProcessButtonSubsystem.ManagerProperty._School,
                    ProcessButtonSubsystem);
            }
        }
    }
}
