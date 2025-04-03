using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.SSM;
using Constructs;
using MyApp.Core.Constructs;
using System;
using System.Collections.Generic;

namespace MyApp.Environments
{
    public abstract class EnvironmentStack : Stack
    {
        protected readonly NetworkConstruct Network;
        protected readonly SecurityConstruct Security;
        protected readonly ComputeConstruct Compute;
        // protected readonly MonitoringConstruct Monitoring;
        protected readonly string EnvironmentName;
        protected readonly string RegionName;

        protected EnvironmentStack(Construct scope, string id, string environmentName, string region, IStackProps props = null)
            : base(scope, id, props)
        {
            EnvironmentName = environmentName;
            RegionName = region;

            // Get container image from Parameter Store (populated by CI/CD pipeline)
            string imageTag;
            try
            {
                imageTag = StringParameter.ValueFromLookup(this, "/myapp/image-tag");
            }
            catch
            {
                // Fallback to latest tag if parameter doesn't exist yet
                imageTag = "latest";
            }

            string repositoryUri = StringParameter.ValueFromLookup(this, "/myapp/ecr-repository-uri");
            string containerImage = $"{repositoryUri}:{imageTag}";

            // Load environment-specific configuration
            var appConfig = new Dictionary<string, Dictionary<string, object>>
            {
                ["Staging"] = new Dictionary<string, object>
                {
                    ["DesiredCount"] = 2,
                    ["CpuUnits"] = 512,
                    ["MemoryLimitMiB"] = 1024,
                    ["AutoScalingMaxCapacity"] = 4,
                    ["EnableWAF"] = false,
                    ["EnableCloudFront"] = false,
                    ["LogRetentionDays"] = 7,
                    ["AlarmThreshold"] = 90.0,
                    ["EnableDetailedMonitoring"] = false
                },
                ["Production"] = new Dictionary<string, object>
                {
                    ["DesiredCount"] = 3,
                    ["CpuUnits"] = 1024,
                    ["MemoryLimitMiB"] = 2048,
                    ["AutoScalingMaxCapacity"] = 10,
                    ["EnableWAF"] = true,
                    ["EnableCloudFront"] = true,
                    ["LogRetentionDays"] = 30,
                    ["AlarmThreshold"] = 80.0,
                    ["EnableDetailedMonitoring"] = true
                }
            };

            // Create network infrastructure
            Network = new NetworkConstruct(this, "Network", new NetworkConstructProps
            {
                EnvironmentName = environmentName,
                RegionName = region,
                Cidr = 16,
                MaxAzs = 3
            });

            // Create security resources
            Security = new SecurityConstruct(this, "Security", environmentName, region);

            // Create monitoring resources first so they can be referenced
            // Monitoring = new MonitoringConstruct(this, "Monitoring", new MonitoringConstructProps
            // {
            //     EnvironmentName = environmentName,
            //     RegionName = region,
            //     DetailedMonitoring = (bool)appConfig[environmentName]["EnableDetailedMonitoring"],
            //     LogRetentionDays = (int)appConfig[environmentName]["LogRetentionDays"],
            //     AlarmThreshold = (double)appConfig[environmentName]["AlarmThreshold"],
            //     AlarmEmailAddresses = new List<string> { "alerts@example.com" }
            // });

            // Create compute resources
            Compute = new ComputeConstruct(this, "Compute", new ComputeConstructProps
            {
                EnvironmentName = environmentName,
                RegionName = region,
                Vpc = Network.Vpc,
                ContainerImage = containerImage,
                ContainerPort = 80,
                DesiredCount = (int)appConfig[environmentName]["DesiredCount"],
                CpuUnits = (int)appConfig[environmentName]["CpuUnits"],
                MemoryLimitMiB = (int)appConfig[environmentName]["MemoryLimitMiB"],
                AutoScalingMaxCapacity = (int)appConfig[environmentName]["AutoScalingMaxCapacity"],
                TaskRole = Security.TaskRole,
                ExecutionRole = Security.ExecutionRole,
                // LogGroup = Monitoring.ApplicationLogGroup,
                EnableWAF = (bool)appConfig[environmentName]["EnableWAF"]
            });

            // Connect monitoring to compute resources
            // Monitoring.MonitorService(Compute.FargateService);

            // Store the Load Balancer DNS in Parameter Store for easy access by CI/CD pipeline
            new StringParameter(this, $"{environmentName}{region}LoadBalancerDns", new StringParameterProps
            {
                ParameterName = $"/myapp/{environmentName}/{region}/loadbalancer-dns",
                StringValue = Compute.FargateService.LoadBalancer.LoadBalancerDnsName,
                Description = $"DNS name for the load balancer in {environmentName} environment, {region} region"
            });

            // Add environment-specific tags
            // Fixed code:
            Amazon.CDK.Tags.Of(this).Add("Environment", environmentName);
            Amazon.CDK.Tags.Of(this).Add("Region", region);
            Amazon.CDK.Tags.Of(this).Add("Project", "MyApp");
            Amazon.CDK.Tags.Of(this).Add("DeploymentTime", DateTime.UtcNow.ToString("yyyy-MM-dd-HH-mm-ss"));
        }
    }
}