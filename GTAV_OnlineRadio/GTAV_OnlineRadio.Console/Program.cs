using GTAV_OnlineRadio.AsiLibrary;
using System;

namespace GTAV_OnlineRadio.TestConsolePlayer
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.CursorVisible = false;

            PrintUsage();
            if (!LoadRadios())
            {
                Console.ReadKey(true);
                return;
            }

            RadioTuner.Instance.Play();
            var newStation = RadioTuner.Instance.CurrentStation;

            PrintCurrentRadio();

            while (true)
            {
                switch (Console.ReadKey(true).Key)
                {
                    case ConsoleKey.LeftArrow:
                        newStation = RadioTuner.Instance.MoveToPreviousStation();

                        Console.CursorLeft = 0;
                        Console.Write(String.Format("Previous station: {0}", newStation).PadRight(Console.BufferWidth));
                        Console.SetCursorPosition(0, Console.CursorTop - 1);
                        break;

                    case ConsoleKey.RightArrow:
                        newStation = RadioTuner.Instance.MoveToNextStation();

                        Console.CursorLeft = 0;
                        Console.Write(String.Format("Next station: {0}", newStation).PadRight(Console.BufferWidth));
                        Console.SetCursorPosition(0, Console.CursorTop - 1);
                        break;

                    case ConsoleKey.Enter:
                        RadioTuner.Instance.ActivateNextStation();
                        RadioTuner.Instance.LogCurrentTrack();

                        PrintCurrentRadio();
                        break;

                    case ConsoleKey.P:
                        if (RadioTuner.Instance.IsRadioOn)
                        {
                            Console.WriteLine("Radio paused.");
                            RadioTuner.Instance.PauseCurrent();
                        }
                        else
                        {
                            Console.WriteLine("Radio restarted.");
                            RadioTuner.Instance.Play();
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

        static void PrintCurrentRadio()
        {
            var station = RadioTuner.Instance.CurrentStation;
            Console.WriteLine("Current radio: {0}", station);
            Console.WriteLine("Current track: {0}", station.CurrentTrackMetaData);
        }

        static bool LoadRadios()
        {
            Console.Write("Waiting for radios to be loaded... ");
            RadioTuner.Instance.LoadRadios();

            bool hasRadios = RadioTuner.Instance.HasRadios;
            if (hasRadios)
            {

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Loading finished successfully.");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Loading finished successfully, but no radios were found.");
            }

            Console.ResetColor();

            return hasRadios;
        }
    }
}
