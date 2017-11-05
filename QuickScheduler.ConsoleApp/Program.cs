using System;

namespace QuickScheduler.ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            new QuartzScheduler(SchedulerConfiguration.Default).Schedule();

            Console.ReadLine();
        }
    }
}
