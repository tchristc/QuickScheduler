using System;

namespace QuickScheduler.ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            new QuartzScheduler(SchedulerConfiguration.Default).Schedule();
            new QuartzScheduler(new SchedulerConfiguration(SchedulerConfiguration.Default) { JobValue = "Boop", TriggerValue="5" }).Schedule();

            Console.ReadLine();
        }
    }
}
