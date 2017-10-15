using GTAV_OnlineRadio.AsiLibrary;
using System;
using System.Threading;

namespace GTAV_OnlineRadio.Console
{
    class Program
    {
        static void Main(string[] args)
        {
            while (RadioTuner.Instance.IsRadioListLoading)
            {
                System.Console.WriteLine("Waiting for radios to be loaded...");
                Thread.Sleep(1000);
            }

            if (!RadioTuner.Instance.HasRadios)
            {
                System.Console.WriteLine("No radios were found.");
                return;
            }

            RadioTuner.Instance.Play();
            var newStation = RadioTuner.Instance.CurrentStation;

            while (true)
            {
                System.Console.WriteLine(newStation.Name ?? "??");

                switch (System.Console.ReadKey().Key)
                {
                    case ConsoleKey.LeftArrow:
                        newStation = RadioTuner.Instance.MoveToPreviousStation();
                        break;
                    case ConsoleKey.RightArrow:
                        newStation = RadioTuner.Instance.MoveToNextStation();
                        break;
                    case ConsoleKey.Enter:
                        RadioTuner.Instance.ActivateNextStation();
                        RadioTuner.Instance.LogCurrentTrack();
                        System.Console.WriteLine(RadioTuner.Instance.CurrentStation.CurrentTrackMetaData);
                        break;
                    case ConsoleKey.P:
                        if (RadioTuner.Instance.IsRadioOn)
                        {
                            RadioTuner.Instance.PauseCurrent();
                        }
                        else
                        {
                            RadioTuner.Instance.Play();
                        }
                        break;
                }
            }
        }
    }
}
