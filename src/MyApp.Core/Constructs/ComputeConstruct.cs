using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.ECS.Patterns;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.WAFv2;
using Constructs;
using System.Collections.Generic;

namespace MyApp.Core.Constructs
{
    public class ComputeConstructProps
    {
        public string EnvironmentName { get; set; }
        public string RegionName { get; set; }
        public Vpc Vpc { get; set; }
        public string ContainerImage { get; set; }
        public int ContainerPort { get; set; } = 80;
        public int DesiredCount { get; set; } = 2;
        public double CpuUnits { get; set; } = 512;
        public int MemoryLimitMiB { get; set; } = 1024;
        public int AutoScalingMaxCapacity { get; set; } = 4;
        public Role TaskRole { get; set; }
        public Role ExecutionRole { get; set; }
        public LogGroup LogGroup { get; set; }
        public bool EnableWAF { get; set; } = false;
    }

    public class ComputeConstruct : Construct
    {
        public ApplicationLoadBalancedFargateService FargateService { get; }
        public CfnWebACLAssociation WafAssociation { get; }

        public ComputeConstruct(Construct scope, string id, ComputeConstructProps props) : base(scope, id)
        {
            // Create ECS Cluster
            var cluster = new Cluster(this, $"{props.EnvironmentName}Cluster", new ClusterProps
            {
                Vpc = props.Vpc,
                ContainerInsights = true,
                ClusterName = $"myapp-{props.EnvironmentName.ToLower()}-{props.RegionName}"
            });

            // Create Fargate Service with ALB
            FargateService = new ApplicationLoadBalancedFargateService(this, $"{props.EnvironmentName}Service", new ApplicationLoadBalancedFargateServiceProps
            {
                Cluster = cluster,
                MemoryLimitMiB = props.MemoryLimitMiB,
                Cpu = (int)props.CpuUnits,
                DesiredCount = props.DesiredCount,
                TaskImageOptions = new ApplicationLoadBalancedTaskImageOptions
                {
                    Image = ContainerImage.FromRegistry(props.ContainerImage),
                    ContainerPort = props.ContainerPort,
                    TaskRole = props.TaskRole,
                    ExecutionRole = props.ExecutionRole,
                    LogDriver = LogDriver.AwsLogs(new AwsLogDriverProps
                    {
                        LogGroup = props.LogGroup,
                        StreamPrefix = props.EnvironmentName.ToLower()
                    }),
                    Environment = new Dictionary<string, string>
                    {
                        { "ENVIRONMENT", props.EnvironmentName },
                        { "REGION", props.RegionName }
                    }
                },
                PublicLoadBalancer = true,
                // Use private subnets for tasks
                TaskSubnets = new SubnetSelection
                {
                    SubnetType = SubnetType.PRIVATE_WITH_EGRESS
                },
                CircuitBreaker = new DeploymentCircuitBreaker
                {
                    Rollback = true
                },
                ServiceName = $"myapp-{props.EnvironmentName.ToLower()}-{props.RegionName}",
                LoadBalancerName = $"myapp-{props.EnvironmentName.ToLower()}-{props.RegionName}"
            });

            // Add scaling policies
            var scaling = FargateService.Service.AutoScaleTaskCount(new Amazon.CDK.AWS.ApplicationAutoScaling.EnableScalingProps
            {
                MinCapacity = props.DesiredCount,
                MaxCapacity = props.AutoScalingMaxCapacity
            });

            scaling.ScaleOnCpuUtilization($"{props.EnvironmentName}CpuScaling", new CpuUtilizationScalingProps
            {
                TargetUtilizationPercent = 70,
                ScaleInCooldown = Duration.Seconds(60),
                ScaleOutCooldown = Duration.Seconds(60)
            });

            // Add memory scaling for high memory applications
            scaling.ScaleOnMemoryUtilization($"{props.EnvironmentName}MemoryScaling", new MemoryUtilizationScalingProps
            {
                TargetUtilizationPercent = 70,
                ScaleInCooldown = Duration.Seconds(60),
                ScaleOutCooldown = Duration.Seconds(60)
            });

            // Add health check
            FargateService.TargetGroup.ConfigureHealthCheck(new Amazon.CDK.AWS.ElasticLoadBalancingV2.HealthCheck
            {
                Path = "/health",
                Timeout = Duration.Seconds(5),
                HealthyHttpCodes = props.EnvironmentName.ToLower() == "production" ? "200" : "200,302", // More permissive for staging
                HealthyThresholdCount = props.EnvironmentName.ToLower() == "production" ? 3 : 2,
                UnhealthyThresholdCount = props.EnvironmentName.ToLower() == "production" ? 2 : 3,
                Interval = Duration.Seconds(props.EnvironmentName.ToLower() == "production" ? 15 : 30)
            });

            // Add WAF for production
            // WAF Configuration Improvements
// WAF Configuration with Corrected Override Action
if (props.EnableWAF)
{
    // Create WAF Web ACL with corrected default action
    var webAcl = new CfnWebACL(this, $"{props.EnvironmentName}WebACL", new CfnWebACLProps
    {
        Name = $"myapp-{props.EnvironmentName.ToLower()}-{props.RegionName}",
        Scope = "REGIONAL",
        DefaultAction = new CfnWebACL.DefaultActionProperty 
        { 
            // Use the explicit AllowActionProperty
            Allow = new CfnWebACL.AllowActionProperty()
        },
        VisibilityConfig = new CfnWebACL.VisibilityConfigProperty
        {
            CloudWatchMetricsEnabled = true,
            MetricName = $"myapp-{props.EnvironmentName.ToLower()}-{props.RegionName}-WebACLMetrics",
            SampledRequestsEnabled = true
        },
        Rules = new object[]
        {
            // AWS Managed Rules - Core Rule Set
            new CfnWebACL.RuleProperty
            {
                Name = "AWS-AWSManagedRulesCommonRuleSet",
                Priority = 0,
                Statement = new CfnWebACL.StatementProperty
                {
                    ManagedRuleGroupStatement = new CfnWebACL.ManagedRuleGroupStatementProperty
                    {
                        Name = "AWSManagedRulesCommonRuleSet",
                        VendorName = "AWS"
                    }
                },
                OverrideAction = new CfnWebACL.OverrideActionProperty 
                { 
                    // Use a dictionary for None action
                    None = new Dictionary<string, object>()
                },
                VisibilityConfig = new CfnWebACL.VisibilityConfigProperty
                {
                    CloudWatchMetricsEnabled = true,
                    MetricName = "AWS-AWSManagedRulesCommonRuleSet",
                    SampledRequestsEnabled = true
                }
            },
            // Rate limiting rule
            new CfnWebACL.RuleProperty
            {
                Name = "RateLimitRule",
                Priority = 1,
                Statement = new CfnWebACL.StatementProperty
                {
                    RateBasedStatement = new CfnWebACL.RateBasedStatementProperty
                    {
                        Limit = 2000,
                        AggregateKeyType = "IP"
                    }
                },
                Action = new CfnWebACL.RuleActionProperty 
                { 
                    Block = new CfnWebACL.BlockActionProperty()
                },
                VisibilityConfig = new CfnWebACL.VisibilityConfigProperty
                {
                    CloudWatchMetricsEnabled = true,
                    MetricName = "RateLimitRule",
                    SampledRequestsEnabled = true
                }
            },
            // SQL Injection Rule
            new CfnWebACL.RuleProperty
            {
                Name = "AWS-AWSManagedRulesSQLiRuleSet",
                Priority = 2,
                Statement = new CfnWebACL.StatementProperty
                {
                    ManagedRuleGroupStatement = new CfnWebACL.ManagedRuleGroupStatementProperty
                    {
                        Name = "AWSManagedRulesSQLiRuleSet",
                        VendorName = "AWS"
                    }
                },
                OverrideAction = new CfnWebACL.OverrideActionProperty 
                { 
                    // Use a dictionary for None action
                    None = new Dictionary<string, object>()
                },
                VisibilityConfig = new CfnWebACL.VisibilityConfigProperty
                {
                    CloudWatchMetricsEnabled = true,
                    MetricName = "AWS-AWSManagedRulesSQLiRuleSet",
                    SampledRequestsEnabled = true
                }
            }
        }
    });

    // Associate WAF with the ALB
    WafAssociation = new CfnWebACLAssociation(this, $"{props.EnvironmentName}WebACLAssociation", new CfnWebACLAssociationProps
    {
        ResourceArn = FargateService.LoadBalancer.LoadBalancerArn,
        WebAclArn = webAcl.AttrArn
    });
}

            // Tag resources
            Tags.Of(this).Add("Environment", props.EnvironmentName);
            Tags.Of(this).Add("Region", props.RegionName);
            Tags.Of(this).Add("Project", "MyApp");
            Tags.Of(this).Add("ResourceType", "Compute");
        }
    }
}