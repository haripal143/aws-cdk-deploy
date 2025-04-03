using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Logs;
using Constructs;
using System.Collections.Generic;

namespace MyApp.Core.Constructs
{
    public class NetworkConstructProps
    {
        public string EnvironmentName { get; set; }
        public string RegionName { get; set; }
        public int Cidr { get; set; } = 16;
        public int MaxAzs { get; set; } = 3;
    }

    public class NetworkConstruct : Construct
    {
        public Vpc Vpc { get; }
        public ISecurityGroup ApiSecurityGroup { get; }
        public ISecurityGroup DatabaseSecurityGroup { get; }

        public NetworkConstruct(Construct scope, string id, NetworkConstructProps props) : base(scope, id)
        {
            // Create a VPC with public and private subnets
            Vpc = new Vpc(this, $"{props.EnvironmentName}Vpc", new VpcProps
            {
                MaxAzs = props.MaxAzs,
                Cidr = $"10.0.0.0/{props.Cidr}",
                SubnetConfiguration = new ISubnetConfiguration[]
                {
                    new SubnetConfiguration
                    {
                        Name = "Public",
                        SubnetType = SubnetType.PUBLIC,
                        CidrMask = 24
                    },
                    new SubnetConfiguration
                    {
                        Name = "Private",
                        SubnetType = SubnetType.PRIVATE_WITH_EGRESS,
                        CidrMask = 24
                    },
                    new SubnetConfiguration
                    {
                        Name = "Isolated",
                        SubnetType = SubnetType.PRIVATE_ISOLATED,
                        CidrMask = 24
                    }
                },
                NatGateways = props.EnvironmentName.ToLower() == "production" ? props.MaxAzs : 1
            });

            // Add Flow Logs for network traffic analysis
            var flowLogRole = new Role(this, $"{props.EnvironmentName}FlowLogRole", new RoleProps
            {
                AssumedBy = new ServicePrincipal("vpc-flow-logs.amazonaws.com")
            });

            var flowLogGroup = new LogGroup(this, $"{props.EnvironmentName}FlowLogGroup", new LogGroupProps
            {
                Retention = props.EnvironmentName.ToLower() == "production" ? RetentionDays.ONE_MONTH : RetentionDays.ONE_WEEK,
                RemovalPolicy = RemovalPolicy.DESTROY
            });

            Vpc.AddFlowLog($"{props.EnvironmentName}FlowLog", new FlowLogOptions
            {
                Destination = FlowLogDestination.ToCloudWatchLogs(flowLogGroup, flowLogRole),
                TrafficType = FlowLogTrafficType.ALL
            });

            // Create security groups
            ApiSecurityGroup = new SecurityGroup(this, $"{props.EnvironmentName}ApiSecurityGroup", new SecurityGroupProps
            {
                Vpc = Vpc,
                Description = "Security Group for API services",
                AllowAllOutbound = false
            });

            // Allow HTTPS outbound for API services
            ApiSecurityGroup.AddEgressRule(Peer.AnyIpv4(), Port.Tcp(443), "Allow HTTPS outbound traffic");

            // Create database security group
            DatabaseSecurityGroup = new SecurityGroup(this, $"{props.EnvironmentName}DbSecurityGroup", new SecurityGroupProps
            {
                Vpc = Vpc,
                Description = "Security group for database access",
                AllowAllOutbound = false
            });

            // Only allow inbound connections from the API security group
            DatabaseSecurityGroup.AddIngressRule(ApiSecurityGroup, Port.Tcp(3306), "Allow MySQL access from API");

            // Tag resources
            Tags.Of(this).Add("Environment", props.EnvironmentName);
            Tags.Of(this).Add("Region", props.RegionName);
            Tags.Of(this).Add("Project", "MyApp");
            Tags.Of(this).Add("ResourceType", "Network");
        }
    }
}