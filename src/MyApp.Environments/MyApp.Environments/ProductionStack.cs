using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.Route53;
using Amazon.CDK.AWS.Route53.Targets;
using Constructs;
using System;

namespace MyApp.Environments
{
    public class ProductionStack : EnvironmentStack
    {
        public ProductionStack(Construct scope, string id, string region, IStackProps props = null)
            : base(scope, id, "Production", region, props)
        {
            // Production-specific configurations

            // More restrictive security group settings
            var corporateNetwork = Peer.Ipv4("10.0.0.0/8");
            Compute.FargateService.Service.Connections.AllowFrom(
                corporateNetwork as IConnectable,  // Cast to IConnectable
                Port.Tcp(80),
                "Allow only from trusted IP ranges"
            );


            // Stricter health checks for production
            Compute.FargateService.TargetGroup.ConfigureHealthCheck(new Amazon.CDK.AWS.ElasticLoadBalancingV2.HealthCheck
            {
                Path = "/health",
                Timeout = Duration.Seconds(5),
                HealthyHttpCodes = "200", // Only 200 is acceptable in production
                HealthyThresholdCount = 3,
                UnhealthyThresholdCount = 2, // Fail faster in production
                Interval = Duration.Seconds(15) // Check more frequently
            });

            // Set up high availability DNS with Route 53
            try
            {
                var hostedZone = HostedZone.FromLookup(this, "HostedZone", new HostedZoneProviderProps
                {
                    DomainName = "example.com"
                });

                var dnsName = region == "us-east-1" ? "app" : $"app-{region}";

                new ARecord(this, "ProductionDnsRecord", new ARecordProps
                {
                    Zone = hostedZone,
                    RecordName = dnsName,
                    Target = RecordTarget.FromAlias(new LoadBalancerTarget(Compute.FargateService.LoadBalancer)),
                    // Set TTL to 60 seconds for faster failover
                    Ttl = Duration.Seconds(60)
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not set up DNS records: {ex.Message}");
            }

            // Add disaster recovery documentation link
            new CfnOutput(this, "DisasterRecoveryProcedures", new CfnOutputProps
            {
                Value = "https://wiki.example.com/disaster-recovery-procedures",
                Description = "Link to disaster recovery procedures document"
            });
        }
    }
}