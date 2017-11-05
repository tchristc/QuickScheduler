using System;

namespace QuickScheduler.ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            new QuartzScheduler(new SchedulerConfiguration
            {JobName = "Default", SchedulerName = "Default", TriggerName = "Interval", TriggerValue = "1"}).Schedule();

            Console.ReadLine();
        }
    }
}
