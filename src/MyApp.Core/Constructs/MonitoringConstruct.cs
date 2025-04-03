// using Amazon.CDK;
// using Amazon.CDK.AWS.CloudWatch;
// using Amazon.CDK.AWS.CloudWatch.Actions;
// using Amazon.CDK.AWS.SNS;
// using Amazon.CDK.AWS.SNS.Subscriptions;
// using Amazon.CDK.AWS.Logs;
// using Amazon.CDK.AWS.ECS.Patterns;
// using Constructs;
// using System;
// using System.Collections.Generic;

// namespace MyApp.Core.Constructs
// {
//     public class MonitoringConstructProps
//     {
//         public string EnvironmentName { get; set; }
//         public string RegionName { get; set; }
//         public bool DetailedMonitoring { get; set; } = false;
//         public int LogRetentionDays { get; set; } = 7;
//         public double AlarmThreshold { get; set; } = 90.0;
//         public List<string> AlarmEmailAddresses { get; set; } = new List<string>();
//     }

//     public class MonitoringConstruct : Construct
//     {
//         public LogGroup ApplicationLogGroup { get; }
//         public Dashboard Dashboard { get; }
//         public Topic AlarmTopic { get; }

//         public MonitoringConstruct(Construct scope, string id, MonitoringConstructProps props) : base(scope, id)
//         {
//             // Create the log group for application logs
//             ApplicationLogGroup = new LogGroup(this, $"{props.EnvironmentName}LogGroup", new LogGroupProps
//             {
//                 LogGroupName = $"/aws/ecs/myapp-{props.EnvironmentName.ToLower()}-{props.RegionName}",
//                 Retention = props.LogRetentionDays == 7 ? RetentionDays.ONE_WEEK :
//                           props.LogRetentionDays == 14 ? RetentionDays.TWO_WEEKS :
//                           props.LogRetentionDays == 30 ? RetentionDays.ONE_MONTH :
//                           props.LogRetentionDays == 90 ? RetentionDays.THREE_MONTHS :
//                           RetentionDays.ONE_WEEK,
//                 RemovalPolicy = RemovalPolicy.DESTROY
//             });

//             // Create an SNS topic for alarms
//             AlarmTopic = new Topic(this, $"{props.EnvironmentName}AlarmTopic", new TopicProps
//             {
//                 DisplayName = $"MyApp-{props.EnvironmentName}-{props.RegionName}-Alarms",
//                 TopicName = $"MyApp-{props.EnvironmentName}-{props.RegionName}-Alarms"
//             });

//             // Add subscriptions if email addresses are provided
//             if (props.AlarmEmailAddresses != null && props.AlarmEmailAddresses.Count > 0)
//             {
//                 foreach (var email in props.AlarmEmailAddresses)
//                 {
//                     AlarmTopic.AddSubscription(new EmailSubscription(email));
//                 }
//             }

//             // Create a CloudWatch Dashboard
//             Dashboard = new Dashboard(this, $"{props.EnvironmentName}Dashboard", new DashboardProps
//             {
//                 DashboardName = $"MyApp{props.EnvironmentName}{props.RegionName}",
//                 PeriodOverride = PeriodOverride.AUTO
//             });

//             // Add default widgets to the dashboard
//             Dashboard.AddWidgets(
//                 new TextWidget(new TextWidgetProps
//                 {
//                     Markdown = $"# MyApp {props.EnvironmentName} Environment ({props.RegionName})\n" +
//                               $"Last updated: {DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm")} UTC",
//                     Width = 24,
//                     Height = 2
//                 })
//             );

//             // Add a health status widget
//             Dashboard.AddWidgets(
//                 new AlarmStatusWidget(new AlarmStatusWidgetProps
//                 {
//                     Title = "Alarms Status",
//                     Width = 24,
//                     Height = 3
//                 })
//             );

//             // Add log insights widget if detailed monitoring is enabled
//             if (props.DetailedMonitoring)
//             {
//                 Dashboard.AddWidgets(
//                     new LogQueryWidget(new LogQueryWidgetProps
//                     {
//                         LogGroupNames = new[] { ApplicationLogGroup.LogGroupName },
//                         Title = "Error Logs (Last 3 Hours)",
//                         Width = 24,
//                         Height = 6,
//                         QueryString = "fields @timestamp, @message\n| filter @message like /ERROR|Error|error|Exception|exception/\n| sort @timestamp desc\n| limit 100",
//                         View = LogQueryVisualizationType.TABLE
//                     })
//                 );
//             }

//             // Tag resources
//             Tags.Of(this).Add("Environment", props.EnvironmentName);
//             Tags.Of(this).Add("Region", props.RegionName);
//             Tags.Of(this).Add("Project", "MyApp");
//             Tags.Of(this).Add("ResourceType", "Monitoring");
//         }

//         // Method to add service monitoring to an existing service
//         public void MonitorService(ApplicationLoadBalancedFargateService service)
//         {
//             // Add common service metrics to dashboard
//             Dashboard.AddWidgets(
//                 new GraphWidget(new GraphWidgetProps
//                 {
//                     Title = "Service CPU/Memory Utilization",
//                     Left = new[] 
//                     { 
//                         service.Service.MetricCpuUtilization(), 
//                         service.Service.MetricMemoryUtilization() 
//                     },
//                     Width = 12,
//                     Height = 6
//                 }),

//                 new GraphWidget(new GraphWidgetProps
//                 {
//                     Title = "Load Balancer Requests",
//                     Left = new[] 
//                     { 
//                         service.LoadBalancer.MetricRequestCount(),
//                         new Metric(new MetricProps
//                         {
//                             Namespace = "AWS/ApplicationELB",
//                             MetricName = "HTTPCode_Target_5XX_Count",
//                             DimensionsMap = new Dictionary<string, string>
//                             {
//                                 { "LoadBalancer", service.LoadBalancer.LoadBalancerFullName }
//                             },
//                             Statistic = "Sum",
//                             Period = Duration.Minutes(1)
//                         })
//                     },
//                     Width = 12,
//                     Height = 6
//                 })
//             );

//             // Add target group metrics
//             Dashboard.AddWidgets(
//                 new GraphWidget(new GraphWidgetProps
//                 {
//                     Title = "Target Group Health",
//                     Left = new[] 
//                     { 
//                         service.TargetGroup.MetricHealthyHostCount(),
//                         service.TargetGroup.MetricUnhealthyHostCount()
//                     },
//                     Width = 12,
//                     Height = 6
//                 }),

//                 new GraphWidget(new GraphWidgetProps
//                 {
//                     Title = "Target Response Time",
//                     Left = new[] { service.TargetGroup.MetricTargetResponseTime() },
//                     Width = 12,
//                     Height = 6
//                 })
//             );

//             // Create alarms for critical metrics

//             // CPU Utilization Alarm
//             var cpuAlarm = new Alarm(this, "ServiceCPUAlarm", new AlarmProps
//             {
//                 AlarmName = $"MyApp-{service.Node.Path}-CPU-Utilization",
//                 AlarmDescription = $"Alarm if CPU utilization exceeds threshold",
//                 Metric = service.Service.MetricCpuUtilization(),
//                 Threshold = 80.0,
//                 EvaluationPeriods = 3,
//                 DatapointsToAlarm = 2,
//                 ComparisonOperator = ComparisonOperator.GREATER_THAN_THRESHOLD,
//                 TreatMissingData = TreatMissingData.NOT_BREACHING
//             });

//             cpuAlarm.AddAlarmAction(new SnsAction(AlarmTopic));
//             cpuAlarm.AddOkAction(new SnsAction(AlarmTopic));

//             // Memory Utilization Alarm
//             var memoryAlarm = new Alarm(this, "ServiceMemoryAlarm", new AlarmProps
//             {
//                 AlarmName = $"MyApp-{service.Node.Path}-Memory-Utilization",
//                 AlarmDescription = $"Alarm if memory utilization exceeds threshold",
//                 Metric = service.Service.MetricMemoryUtilization(),
//                 Threshold = 80.0,
//                 EvaluationPeriods = 3,
//                 DatapointsToAlarm = 2,
//                 ComparisonOperator = ComparisonOperator.GREATER_THAN_THRESHOLD,
//                 TreatMissingData = TreatMissingData.NOT_BREACHING
//             });

//             memoryAlarm.AddAlarmAction(new SnsAction(AlarmTopic));
//             memoryAlarm.AddOkAction(new SnsAction(AlarmTopic));

//             // HTTP 5XX Errors Alarm
//             var http5xxMetric = new Metric(new MetricProps
//             {
//                 Namespace = "AWS/ApplicationELB",
//                 MetricName = "HTTPCode_Target_5XX_Count",
//                 DimensionsMap = new Dictionary<string, string>
//                 {
//                     { "LoadBalancer", service.LoadBalancer.LoadBalancerFullName }
//                 },
//                 Statistic = "Sum",
//                 Period = Duration.Minutes(1)
//             });

//             var http5xxAlarm = new Alarm(this, "Http5xxAlarm", new AlarmProps
//             {
//                 AlarmName = $"MyApp-{service.Node.Path}-HTTP-5XX",
//                 AlarmDescription = $"Alarm if 5XX error count exceeds threshold",
//                 Metric = http5xxMetric,
//                 Threshold = 10,
//                 EvaluationPeriods = 2,
//                 DatapointsToAlarm = 2,
//                 TreatMissingData = TreatMissingData.NOT_BREACHING
//             });

//             http5xxAlarm.AddAlarmAction(new SnsAction(AlarmTopic));
//             http5xxAlarm.AddOkAction(new SnsAction(AlarmTopic));

//             // Unhealthy Host Count Alarm
//             var unhealthyHostAlarm = new Alarm(this, "UnhealthyHostAlarm", new AlarmProps
//             {
//                 AlarmName = $"MyApp-{service.Node.Path}-Unhealthy-Hosts",
//                 AlarmDescription = $"Alarm if there are unhealthy hosts",
//                 Metric = service.TargetGroup.MetricUnhealthyHostCount(),
//                 Threshold = 1,
//                 EvaluationPeriods = 2,
//                 DatapointsToAlarm = 2,
//                 ComparisonOperator = ComparisonOperator.GREATER_THAN_OR_EQUAL_TO_THRESHOLD,
//                 TreatMissingData = TreatMissingData.BREACHING
//             });

//             unhealthyHostAlarm.AddAlarmAction(new SnsAction(AlarmTopic));
//             unhealthyHostAlarm.AddOkAction(new SnsAction(AlarmTopic));

//             // Response Time Alarm
//             var responseTimeAlarm = new Alarm(this, "ResponseTimeAlarm", new AlarmProps
//             {
//                 AlarmName = $"MyApp-{service.Node.Path}-Response-Time",
//                 AlarmDescription = $"Alarm if response time exceeds threshold",
//                 Metric = service.TargetGroup.MetricTargetResponseTime(),
//                 Threshold = 2, // 2 seconds
//                 EvaluationPeriods = 3,
//                 DatapointsToAlarm = 2,
//                 ComparisonOperator = ComparisonOperator.GREATER_THAN_THRESHOLD,
//                 TreatMissingData = TreatMissingData.NOT_BREACHING
//             });

//             responseTimeAlarm.AddAlarmAction(new SnsAction(AlarmTopic));
//             responseTimeAlarm.AddOkAction(new SnsAction(AlarmTopic));

//             // Create CloudWatch Dashboard annotations for alarm thresholds
//             Dashboard.AddWidgets(
//                 new SingleValueWidget(new SingleValueWidgetProps
//                 {
//                     Title = "Service Health Overview",
//                     Metrics = new[] {
//                         service.Service.MetricCpuUtilization(),
//                         service.Service.MetricMemoryUtilization(),
//                         service.TargetGroup.MetricTargetResponseTime()
//                     },
//                     Width = 24,
//                     Height = 3
//                 })
//             );

//             // Add application logs insight if detailed monitoring is enabled
//             if (System.Environment.GetEnvironmentVariable("DETAILED_MONITORING")?.ToLower() == "true")
//             {
//                 Dashboard.AddWidgets(
//                     new LogQueryWidget(new LogQueryWidgetProps
//                     {
//                         LogGroupNames = new[] { ApplicationLogGroup.LogGroupName },
//                         Title = "Application Errors",
//                         Width = 24,
//                         Height = 6,
//                         QueryString = "fields @timestamp, @message | filter @message like /ERROR|Exception/ | sort @timestamp desc | limit 20",
//                         View = LogQueryVisualizationType.TABLE
//                     })
//                 );
//             }
//         }

//         // Methods to add custom metrics and alarms
//         public Alarm CreateCustomAlarm(string alarmName, IMetric metric, double threshold, int evaluationPeriods = 3)
//         {
//             var alarm = new Alarm(this, alarmName, new AlarmProps
//             {
//                 AlarmName = alarmName,
//                 AlarmDescription = $"Custom alarm for {alarmName}",
//                 Metric = metric,
//                 Threshold = threshold,
//                 EvaluationPeriods = evaluationPeriods,
//                 DatapointsToAlarm = Math.Max(1, evaluationPeriods / 2),
//                 ComparisonOperator = ComparisonOperator.GREATER_THAN_THRESHOLD,
//                 TreatMissingData = TreatMissingData.NOT_BREACHING
//             });

//             alarm.AddAlarmAction(new SnsAction(AlarmTopic));
//             alarm.AddOkAction(new SnsAction(AlarmTopic));

//             return alarm;
//         }

//         // Method to add custom metrics to the dashboard
//         public void AddMetricToDashboard(string title, IMetric metric, int width = 12, int height = 6)
//         {
//             Dashboard.AddWidgets(
//                 new GraphWidget(new GraphWidgetProps
//                 {
//                     Title = title,
//                     Left = new[] { metric },
//                     Width = width,
//                     Height = height
//                 })
//             );
//         }

//         // Method to create a composite alarm from multiple other alarms
//         public CompositeAlarm CreateCompositeAlarm(string alarmName, Alarm[] alarms, bool useAllOf = true)
//         {
//             var alarmRule = useAllOf ? AlarmRule.AllOf(alarms) : AlarmRule.AnyOf(alarms);

//             var compositeAlarm = new CompositeAlarm(this, alarmName, new CompositeAlarmProps
//             {
//                 CompositeAlarmName = alarmName,
//                 AlarmRule = alarmRule,
//                 ActionsEnabled = true
//             });

//             compositeAlarm.AddAlarmAction(new SnsAction(AlarmTopic));
//             compositeAlarm.AddOkAction(new SnsAction(AlarmTopic));

//             return compositeAlarm;
//         }
//     }
// }