using RadioExpansion.Core;
using RadioExpansion.Core.Logging;
using RadioExpansion.Core.RadioPlayers;
using System;

namespace RadioExpansion.TestConsolePlayer
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.CursorVisible = false;

            PrintUsage();

            Logger.SetLogger(new ConsoleLogger());

            var radios = LoadRadios();
            if (radios.Length == 0)
            {
                Console.ReadKey(true);
                return;
            }

            var radioTuner = new RadioTuner(radios);

            radioTuner.Play();
            var newStation = radioTuner.CurrentStation;

            PrintCurrentRadio(radioTuner);

            while (true)
            {
                switch (Console.ReadKey(true).Key)
                {
                    case ConsoleKey.LeftArrow:
                        newStation = radioTuner.MoveToPreviousStation();

                        Console.CursorLeft = 0;
                        Console.Write(String.Format("Previous station: {0}", newStation).PadRight(Console.BufferWidth));
                        Console.SetCursorPosition(0, Console.CursorTop - 1);
                        break;

                    case ConsoleKey.RightArrow:
                        newStation = radioTuner.MoveToNextStation();

                        Console.CursorLeft = 0;
                        Console.Write(String.Format("Next station: {0}", newStation).PadRight(Console.BufferWidth));
                        Console.SetCursorPosition(0, Console.CursorTop - 1);
                        break;

                    case ConsoleKey.Enter:
                        radioTuner.ActivateNextStation();
                        Logger.LogTrack(radioTuner.CurrentStation.Name, radioTuner.CurrentStation.CurrentTrackMetaData);

                        PrintCurrentRadio(radioTuner);
                        break;

                    case ConsoleKey.P:
                        if (radioTuner.IsRadioOn)
                        {
                            Console.WriteLine("Radio paused.");
                            radioTuner.PauseCurrent();
                        }
                        else
                        {
                            Console.WriteLine("Radio restarted.");
                            radioTuner.Play();
                        }
                        break;

                    case ConsoleKey.Escape:
                        return;
                }
            }
        }

        static void PrintUsage()
        {
            Console.WriteLine("Usage:{0}", Environment.NewLine);
            Console.WriteLine("Right arrow: next station.");
            Console.WriteLine("Left arrow: previous station.");
            Console.WriteLine("Enter: activates next station, or prints the metadata of the current station.");
            Console.WriteLine("P: pauses/restarts current station.");
            Console.WriteLine("Escape: exit.");
            Console.WriteLine(Environment.NewLine);
        }

        static void PrintCurrentRadio(RadioTuner radioTuner)
        {
            var station = radioTuner.CurrentStation;
            Console.WriteLine($"Current radio: {station}".PadRight(Console.BufferWidth - 1));
            Console.WriteLine("Current track: {0}", station.CurrentTrackMetaData);
        }

        static Radio[] LoadRadios()
        {
            Console.WriteLine("Waiting for radios to be loaded... ");
            
            var radios = RadioConfigManager.LoadConfig()?.Radios;

            if (radios != null)
            {
                if (radios.Length > 0)
                {

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Loading finished successfully.");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Loading finished successfully, but no radios were found.");
                }
            }

            Console.ResetColor();

            return radios;
        }
    }
}
