using System;
using System.Collections.Generic;
using Quartz;

namespace QuickScheduler
{
    internal static class EnumExtension
    {
        internal static string GetName<T>(this T e)
            where T : struct, IConvertible
        {
            if (!typeof(T).IsEnum)
            {
                throw new ArgumentException("T must be an enumerated type");
            }
            return Enum.GetName(typeof(T), e);
        }
    }

    public enum TriggerName
    {
        Cron = 1,
        Interval = 2
    }

    public interface IProvider<out T>
    {
        T Provide();
    }

    public interface ISchedulerProvider : IProvider<IScheduler> { }
    public interface ITriggerProvider : IProvider<ITrigger> { string Name { get; } }
    public interface IJobDetailProvider : IProvider<IJobDetail> { }

    public class SchedulerConfiguration
    {
        public string SchedulerName { get; set; }
        public string TriggerName { get; set; }
        public string TriggerValue { get; set; }
    }

    public abstract class SchedulerEntity
    {
        protected SchedulerConfiguration Configuration { get; set; }
        protected SchedulerEntity(SchedulerConfiguration configuration)
        {
            Configuration = configuration;
        }

        protected string SchedulerGroup => $"{Configuration.SchedulerName}_Group";
    }

    public abstract class TriggerProviderStrategy : SchedulerEntity, ITriggerProvider
    {
        public abstract string Name { get; }

        protected TriggerProviderStrategy(SchedulerConfiguration configuration)
            : base(configuration)
        { }

        protected string TriggerIdentityName => $"{Configuration.SchedulerName}_{Configuration.TriggerName}_Trigger";

        public abstract ITrigger Provide();
    }

    public interface ITriggerProviderStrategyCron : ITriggerProvider { }

    public class TriggerProviderStrategyCron : TriggerProviderStrategy, ITriggerProviderStrategyCron
    {
        public override string Name => TriggerName.Cron.GetName();

        public TriggerProviderStrategyCron(SchedulerConfiguration configuration)
            : base(configuration)
        { }

        public override ITrigger Provide()
        {
            if (Configuration.TriggerValue == null)
                throw new NullReferenceException($"TriggerValue was not formatted as cron (ex. \"0 0 12 ? * *\"). The value was NULL.");

            //Seconds Minutes Hours DayOfMonth Month DayOfWeek Year
            //“0 0 12 ? * * ” = “everyday at 12:00 pm” (values are zero indexed)
            var trigger = TriggerBuilder.Create()
              .WithIdentity(TriggerIdentityName, SchedulerGroup)
              .WithCronSchedule(Configuration.TriggerValue)
              .Build();
            return trigger;
        }
    }

    public interface ITriggerProviderStrategyInterval : ITriggerProvider { }

    public class TriggerProviderStrategyInterval : TriggerProviderStrategy, ITriggerProviderStrategyInterval
    {
        public override string Name => TriggerName.Interval.GetName();

        public TriggerProviderStrategyInterval(SchedulerConfiguration configuration)
            : base(configuration)
        { }

        public override ITrigger Provide()
        {
            if (Configuration.TriggerValue == null)
                throw new NullReferenceException($"TriggerValue was not formatted as an int (ex. 5). The value was NULL.");

            int interval;
            if (!int.TryParse(Configuration.TriggerValue, out interval))
                throw new FormatException($"TriggerValue was not formatted as an int. The value was {Configuration.TriggerValue}.");

            var trigger = TriggerBuilder.Create()
              .WithIdentity(TriggerIdentityName, SchedulerGroup)
              .StartNow()
              .WithSimpleSchedule(x => x
                  .WithIntervalInSeconds(interval)
                  .RepeatForever())
              .Build();

            return trigger;
        }
    }

    public class TriggerProviderStrategyFactory
    {
        public Dictionary<string, ITriggerProvider> TriggerProviders { get; }

        public TriggerProviderStrategyFactory(ITriggerProviderStrategyCron triggerProviderStrategyCron, ITriggerProviderStrategyInterval triggerProviderStrategyInterval)
        {
            TriggerProviders = new Dictionary<string, ITriggerProvider>();
            Register(triggerProviderStrategyCron);
            Register(triggerProviderStrategyInterval);
        }

        public void Register(ITriggerProvider triggerProvider)
        {
            TriggerProviders.Add(triggerProvider.Name, triggerProvider);
        }

        public ITriggerProvider Get(string triggerName)
        {
            if (triggerName == null)
            {
                throw new ArgumentNullException(nameof(triggerName));
            }

            if (!TriggerProviders.ContainsKey(triggerName))
            {
                throw new ArgumentException($"triggerName \"{triggerName}\" is not registered.");    
            }

            return TriggerProviders[triggerName];
        }
    }

    public interface IQuartzScheduler
    {
        void Schedule(SchedulerConfiguration schedulerConfiguration);
        void Reschedule(SchedulerConfiguration schedulerConfiguration);
    }

    public abstract class QuartzScheduler : IQuartzScheduler
    {
        private readonly TriggerProviderStrategyFactory _triggerProviderStrategyFactory;
        private readonly IJobDetailProvider _jobDetailProvider;
        private readonly ISchedulerProvider _schedulerProvider;

        public abstract string TriggerName { get; }
        public abstract string GroupName { get; }

        protected QuartzScheduler(TriggerProviderStrategyFactory triggerProviderStrategyFactory, IJobDetailProvider jobDetailProvider, ISchedulerProvider schedulerProvider)
        {
            _triggerProviderStrategyFactory = triggerProviderStrategyFactory;
            _jobDetailProvider = jobDetailProvider;
            _schedulerProvider = schedulerProvider;
        }

        public virtual void Schedule(SchedulerConfiguration schedulerConfiguration=null)
        {
            var scheduler = _schedulerProvider.Provide();

            scheduler.Start().Wait();

            var job = _jobDetailProvider.Provide();

            var triggerProvider = _triggerProviderStrategyFactory.Get(schedulerConfiguration?.TriggerName);
            var trigger = triggerProvider.Provide();

            scheduler.ScheduleJob(job, trigger).Wait();
        }

        public virtual void Reschedule(SchedulerConfiguration schedulerConfiguration=null)
        {
            var triggerProvider = _triggerProviderStrategyFactory.Get(schedulerConfiguration?.TriggerName);

            var scheduler = _schedulerProvider.Provide();
            scheduler.RescheduleJob(
                new TriggerKey(
                    triggerProvider.Name,
                    GroupName),
                triggerProvider.Provide());
        }
    }

    public class SchedulerManager
    {
        public Dictionary<string, QuartzScheduler> SchedulersByName { get; private set; }

        public SchedulerManager()
        {
            SchedulersByName = new Dictionary<string, QuartzScheduler>();
        }
    }
}
