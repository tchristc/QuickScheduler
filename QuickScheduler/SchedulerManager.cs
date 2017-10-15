using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Quartz;
using Quartz.Impl.Triggers;

namespace QuickScheduler
{
    public interface IProvider<out T>
    {
        T Provide();
    }

    public interface ISchedulerProvider : IProvider<IScheduler> { }
    public interface ITriggerProvider : IProvider<ITrigger> { }
    public interface IJobDetailProvider : IProvider<IJobDetail> { }


    public class SchedulerConfiguration
    {
        public string SchedulerName { get; set; }
        public string TriggerStrategyName { get; set; }
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
        protected TriggerProviderStrategy(SchedulerConfiguration configuration)
            : base(configuration)
        { }

        protected string TriggerIdentityName => $"{Configuration.SchedulerName}_{Configuration.TriggerStrategyName}_Trigger";

        public abstract ITrigger Provide();
    }

    public class TriggerProviderStrategyName
    {
        public const string TRIGGER_CRON = "Cron";
        public const string TRIGGER_INTERVAL = "Interval";
    }

    public interface ITriggerProviderStrategyCron : ITriggerProvider { }

    public class TriggerProviderStrategyCron : TriggerProviderStrategy, ITriggerProviderStrategyCron
    {

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

    public class TriggerProviderFactory
    {
        public Dictionary<string, ITriggerProvider> TriggerProviders { get; }

        public TriggerProviderFactory(ITriggerProviderStrategyCron triggerProviderStrategyCron, 
            ITriggerProviderStrategyInterval triggerProviderStrategyInterval)
        {
            TriggerProviders = new Dictionary<string, ITriggerProvider>
            {
                { TriggerProviderStrategyName.TRIGGER_CRON, triggerProviderStrategyCron},
                { TriggerProviderStrategyName.TRIGGER_INTERVAL, triggerProviderStrategyInterval },
            };
        }
    }

    public interface IQuartzScheduler
    {
        void Schedule(SchedulerConfiguration schedulerConfiguration);
        void Reschedule(SchedulerConfiguration schedulerConfiguration);
    }

    public abstract class QuartzScheduler : IQuartzScheduler
    {
        private readonly ITriggerProvider _triggerProvider;
        private readonly IJobDetailProvider _jobDetailProvider;
        private readonly ISchedulerProvider _schedulerProvider;

        public abstract string TriggerName { get; }
        public abstract string GroupName { get; }

        protected QuartzScheduler(ITriggerProvider triggerProvider, IJobDetailProvider jobDetailProvider, ISchedulerProvider schedulerProvider)
        {
            _triggerProvider = triggerProvider;
            _jobDetailProvider = jobDetailProvider;
            _schedulerProvider = schedulerProvider;
        }

        public virtual void Schedule(SchedulerConfiguration schedulerConfiguration=null)
        {
            var scheduler = _schedulerProvider.Provide();

            scheduler.Start().Wait();

            var job = _jobDetailProvider.Provide();
            var trigger = _triggerProvider.Provide();

            scheduler.ScheduleJob(job, trigger).Wait();
        }

        public virtual void Reschedule(SchedulerConfiguration schedulerConfiguration=null)
        {
            var scheduler = _schedulerProvider.Provide();
            scheduler.RescheduleJob(
                new TriggerKey(
                    TriggerName,
                    GroupName),
                _triggerProvider.Provide());
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
