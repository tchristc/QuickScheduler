using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Quartz;
using Quartz.Impl;

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
        string Name { get; }
        T Provide();
    }

    public interface ISchedulerProvider : IProvider<IScheduler> { }
    public interface ITriggerProvider : IProvider<ITrigger> { }
    public interface IJobDetailProvider : IProvider<IJobDetail> { }

    public class SchedulerConfiguration
    {
        public string SchedulerName { get; set; }
        public string TriggerName { get; set; }
        public string TriggerValue { get; set; }

        public string JobName { get; set; }
        public string JobValue { get; set; }
        public Guid JobGuid { get; set; } = Guid.NewGuid();

        public static readonly SchedulerConfiguration Default = new SchedulerConfiguration
        {
            JobName = "Default",
            JobValue = "Test",
            SchedulerName = "Default",
            TriggerName = "Interval",
            TriggerValue = "1"
        };

        public SchedulerConfiguration()
        {

        }

        public SchedulerConfiguration(SchedulerConfiguration other)
        {
            JobName = other.JobName;
            JobValue = other.JobValue;
            SchedulerName = other.SchedulerName;
            TriggerName = other.TriggerName;
            TriggerValue = other.TriggerValue;
        }
    }

    public abstract class SchedulerEntity
    {
        protected SchedulerConfiguration Configuration { get; set; }
        protected SchedulerEntity(SchedulerConfiguration configuration)
        {
            Configuration = configuration;
        }

        protected string SchedulerGroupName => $"{Configuration.SchedulerName}_{Configuration.JobGuid}_Group";
        protected string SchedulerJobName => $"{Configuration.SchedulerName}_{Configuration.JobGuid}_Job";
    }

    public abstract class TriggerProvider : SchedulerEntity, ITriggerProvider
    {
        public abstract string Name { get; }

        protected TriggerProvider(SchedulerConfiguration configuration)
            : base(configuration)
        { }

        protected string TriggerIdentityName => $"{Configuration.SchedulerName}_{Configuration.TriggerName}_Trigger";

        public abstract ITrigger Provide();
    }

    public interface ITriggerProviderCron : ITriggerProvider { }

    public class TriggerProviderCron : TriggerProvider, ITriggerProviderCron
    {
        public override string Name => TriggerName.Cron.GetName();

        public TriggerProviderCron(SchedulerConfiguration configuration)
            : base(configuration)
        { }

        public override ITrigger Provide()
        {
            if (Configuration.TriggerValue == null)
                throw new NullReferenceException($"TriggerValue was not formatted as cron (ex. \"0 0 12 ? * *\"). The value was NULL.");

            //Seconds Minutes Hours DayOfMonth Month DayOfWeek Year
            //“0 0 12 ? * * ” = “everyday at 12:00 pm” (values are zero indexed)
            var trigger = TriggerBuilder.Create()
              .WithIdentity(TriggerIdentityName, SchedulerGroupName)
              .WithCronSchedule(Configuration.TriggerValue)
              .Build();
            return trigger;
        }
    }

    public interface ITriggerProviderInterval : ITriggerProvider { }

    public class TriggerProviderInterval : TriggerProvider, ITriggerProviderInterval
    {
        public override string Name => TriggerName.Interval.GetName();

        public TriggerProviderInterval(SchedulerConfiguration configuration)
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
              .WithIdentity(TriggerIdentityName, SchedulerGroupName)
              .StartNow()
              .WithSimpleSchedule(x => x
                  .WithIntervalInSeconds(interval)
                  .RepeatForever())
              .Build();

            return trigger;
        }
    }

    public interface IDefaultJob : IJob { }

    public class DefaultJob : IDefaultJob
    {
        public Task Execute(IJobExecutionContext context)
        {
            try
            {
                var dataMap = context.MergedJobDataMap;
                var schedulerConfiguration = (SchedulerConfiguration)dataMap[nameof(SchedulerConfiguration)];
                Console.WriteLine(schedulerConfiguration.JobValue);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

            return Task.FromResult(0);
        }
    }



    public abstract class JobDetailProvider : SchedulerEntity, IJobDetailProvider
    {
        public abstract string Name { get; }

        protected JobDetailProvider(SchedulerConfiguration configuration)
            : base(configuration)
        { }

        public abstract IJobDetail Provide();
    }

    public interface IDefaultJobDetailProvider : IJobDetailProvider { }

    public class DefaultJobDetailProvider : JobDetailProvider, IDefaultJobDetailProvider
    {
        public override string Name => $"{Configuration.JobName}_{Configuration.JobGuid}";

        public DefaultJobDetailProvider(SchedulerConfiguration configuration)
            : base(configuration)
        { }

        public override IJobDetail Provide()
        {
            var job = JobBuilder.Create<DefaultJob>()
                .WithIdentity(SchedulerJobName, SchedulerGroupName)
                .Build();
            job.JobDataMap.Put(nameof(SchedulerConfiguration), Configuration);

            return job;
        }
    }

    //public class JobQueueConsumerJobTriggerListener : ITriggerListener
    //{
    //    private readonly IJobQueueSettings _jobQueueSettings;
    //    private readonly IApplicationSettingsBusiness _applicationSettingsBusiness;

    //    public JobQueueConsumerJobTriggerListener(IJobQueueSettings jobQueueSettings, IApplicationSettingsBusiness applicationSettingsBusiness)
    //    {
    //        _jobQueueSettings = jobQueueSettings;
    //        _applicationSettingsBusiness = applicationSettingsBusiness;
    //    }

    //    public Task TriggerFired(ITrigger trigger, IJobExecutionContext context)
    //    {
    //        return Task.FromResult(0);
    //    }

    //    public Task<bool> VetoJobExecution(ITrigger trigger, IJobExecutionContext context)
    //    {

    //        var jobs = context.Scheduler.GetCurrentlyExecutingJobs().Result;
    //        foreach (var job in jobs)
    //        {
    //            if (job.Trigger.Equals(context.Trigger) && job.JobInstance != context.JobInstance)
    //            {
    //                //job is already running
    //                return Task.FromResult(true);
    //            }
    //        }

    //        if (
    //            !_applicationSettingsBusiness.IsEnvironmentLocalhost() &&
    //            !_jobQueueSettings.EnableJobQueuePoll)
    //        {
    //            //only quit if NOT localhost and jobQueue Poll is NOT enabled
    //            return Task.FromResult(true);
    //        }

    //        return Task.FromResult(false);
    //    }

    //    public Task TriggerMisfired(ITrigger trigger)
    //    {
    //        return Task.FromResult(0);
    //    }

    //    public Task TriggerComplete(ITrigger trigger, IJobExecutionContext context, SchedulerInstruction triggerInstructionCode)
    //    {
    //        return Task.FromResult(0);
    //    }

    //    public string Name
    //    {
    //        get { return JobQueueConsumerQuartzSchedulerEnums.JOB_QUEUE_CONSUMER_JOB_TRIGGER_LISTENER_NAME; }
    //    }
    //}

    //public class NInjectQuartzJobFactory<T> : IJobFactory
    //    where T : IJob
    //{
    //    private readonly IResolutionRoot _resolutionRoot;

    //    public NInjectQuartzJobFactory(IResolutionRoot resolutionRoot)
    //    {
    //        _resolutionRoot = resolutionRoot;
    //    }

    //    public IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
    //    {
    //        return _resolutionRoot.Get<T>();
    //    }

    //    public void ReturnJob(IJob job)
    //    {
    //        _resolutionRoot.Release(job);
    //    }
    //}

    public abstract class SchedulerProvider : SchedulerEntity, ISchedulerProvider
    {
        public abstract string Name { get; }

        protected SchedulerProvider(SchedulerConfiguration configuration)
            : base(configuration)
        { }

        public abstract IScheduler Provide();
    }

    public interface IDefaultSchedulerProvider :
        ISchedulerProvider
    {
    }

    public class DefaultSchedulerProvider 
        : SchedulerProvider, IDefaultSchedulerProvider
    {
        public override string Name => "Default";

        //private readonly IResolutionRoot _resolutionRoot;
        //private readonly IJobQueueSettings _jobQueueSettings;
        //private readonly IApplicationSettingsBusiness _applicationSettingsBusiness;

        public DefaultSchedulerProvider(SchedulerConfiguration schedulerConfiguration)
            : base(schedulerConfiguration)
        {

        }

        public override IScheduler Provide()
        {
            var schedulerFactory = new StdSchedulerFactory(
                new NameValueCollection
                {
                    { "quartz.scheduler.instanceName", SchedulerJobName }
                });
            var scheduler = schedulerFactory.GetScheduler().Result;

            //scheduler.JobFactory = new NInjectQuartzJobFactory<IJobQueueConsumerQuartzJob>(_resolutionRoot);
            //scheduler.ListenerManager.AddTriggerListener(new JobQueueConsumerJobTriggerListener(_jobQueueSettings, _applicationSettingsBusiness));
            return scheduler;
        }
    }

    public interface IProviderFactory<TProvider, TProvides>
        where TProvider : IProvider<TProvides>
    {
        void Register(TProvider provider);
        TProvider Get(string providerName);
    }

    public abstract class ProviderFactory<TProvider, TProvides> : IProviderFactory<TProvider, TProvides>
        where TProvider : IProvider<TProvides>
    {
        public Dictionary<string, TProvider> Providers { get; } = new Dictionary<string, TProvider>();

        public void Register(TProvider provider)
        {
            Providers.Add(provider.Name, provider);
        }

        public TProvider Get(string providerName)
        {
            //if (providerName == null)
            //{
            //    throw new ArgumentNullException(nameof(providerName));
            //}

            //if (!Providers.ContainsKey(providerName))
            //{
            //    throw new ArgumentException($"providerName \"{providerName}\" is not registered.");
            //}

            if (providerName == null || !Providers.ContainsKey(providerName))
            {
                return Providers.Values.FirstOrDefault();
            }

            return Providers[providerName];
        }
    }

    public class TriggerProviderFactory : ProviderFactory<ITriggerProvider, ITrigger>
    {
        public TriggerProviderFactory(ITriggerProviderCron triggerProviderCron, ITriggerProviderInterval triggerProviderInterval)
        {
            Register(triggerProviderInterval);
            Register(triggerProviderCron);
        }
    }

    public class JobDetailProviderFactory : ProviderFactory<IJobDetailProvider, IJobDetail>
    {
        public JobDetailProviderFactory(IDefaultJobDetailProvider defaultJobDetailProvider)
        {
            Register(defaultJobDetailProvider);
        }
    }

    public class SchedulerProviderFactory : ProviderFactory<ISchedulerProvider, IScheduler>
    {
        public SchedulerProviderFactory(IDefaultSchedulerProvider defaultSchedulerProvider)
        {
            Register(defaultSchedulerProvider);
        }
    }


    public interface IQuartzScheduler
    {
        void Schedule();
        void Reschedule();
    }

    public class QuartzScheduler : SchedulerEntity, IQuartzScheduler
    {
        private readonly TriggerProviderFactory _triggerProviderFactory;
        private readonly JobDetailProviderFactory _jobDetailProviderFactory;
        private readonly SchedulerProviderFactory _schedulerProviderFactory;


        public QuartzScheduler(SchedulerConfiguration configuration,
            TriggerProviderFactory triggerProviderFactory,
            JobDetailProviderFactory jobDetailProviderFactory,
            SchedulerProviderFactory schedulerProviderFactory)
            : base(configuration)
        {
            _triggerProviderFactory = triggerProviderFactory;
            _jobDetailProviderFactory = jobDetailProviderFactory;
            _schedulerProviderFactory = schedulerProviderFactory;
        }

        public QuartzScheduler(SchedulerConfiguration configuration)
            : this(configuration,
               new TriggerProviderFactory(
                    new TriggerProviderCron(configuration),
                    new TriggerProviderInterval(configuration)),
                new JobDetailProviderFactory(new DefaultJobDetailProvider(configuration)),
                new SchedulerProviderFactory(new DefaultSchedulerProvider(configuration)))
        {
        }

        public virtual void Schedule()
        {
            var schedulerProvider = _schedulerProviderFactory.Get(Configuration.SchedulerName);
            var scheduler = schedulerProvider.Provide();

            scheduler.Start().Wait();

            var jobDetailProvider = _jobDetailProviderFactory.Get(Configuration.JobName);
            var job = jobDetailProvider.Provide();

            var triggerProvider = _triggerProviderFactory.Get(Configuration.TriggerName);
            var trigger = triggerProvider.Provide();

            scheduler.ScheduleJob(job, trigger).Wait();
        }

        public virtual void Reschedule()
        {
            var triggerProvider = _triggerProviderFactory.Get(Configuration.TriggerName);
            var schedulerProvider = _schedulerProviderFactory.Get(Configuration.SchedulerName);
            var scheduler = schedulerProvider.Provide();
            scheduler.RescheduleJob(
                new TriggerKey(
                    triggerProvider.Name,
                    SchedulerGroupName),
                triggerProvider.Provide());
        }
    }
}
