using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Constructs;

namespace MyApp.Environments
{
    public class StagingStack : EnvironmentStack
    {
        public StagingStack(Construct scope, string id, string region, IStackProps props = null)
            : base(scope, id, "Staging", region, props)
        {
            // Staging-specific configurations

            // Add wider security group rules for testing
            Compute.FargateService.Service.Connections.AllowFromAnyIpv4(Port.Tcp(80));

            // Add development endpoints
            Compute.FargateService.TargetGroup.ConfigureHealthCheck(new Amazon.CDK.AWS.ElasticLoadBalancingV2.HealthCheck
            {
                Path = "/health",
                Timeout = Duration.Seconds(5),
                HealthyHttpCodes = "200,302", // More permissive for staging
                Interval = Duration.Seconds(30)
            });

            // Export outputs for testing environments
            new CfnOutput(this, "StagingEndpoint", new CfnOutputProps
            {
                Value = Compute.FargateService.LoadBalancer.LoadBalancerDnsName,
                Description = "Endpoint URL for the staging environment",
                ExportName = $"StagingEndpoint-{RegionName}"
            });
        }
    }
}