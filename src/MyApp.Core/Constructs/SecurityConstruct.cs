using Amazon.CDK;
using Amazon.CDK.AWS.IAM;
using Constructs;

namespace MyApp.Core.Constructs
{
    public class SecurityConstruct : Construct
    {
        public Role TaskRole { get; }
        public Role ExecutionRole { get; }

        public SecurityConstruct(Construct scope, string id, string environmentName, string region) : base(scope, id)
        {
            // Create task execution role
            ExecutionRole = new Role(this, $"{environmentName}TaskExecutionRole", new RoleProps
            {
                AssumedBy = new ServicePrincipal("ecs-tasks.amazonaws.com"),
                ManagedPolicies = new IManagedPolicy[]
                {
                    ManagedPolicy.FromAwsManagedPolicyName("service-role/AmazonECSTaskExecutionRolePolicy")
                }
            });

            // Create task role with more specific permissions
            TaskRole = new Role(this, $"{environmentName}TaskRole", new RoleProps
            {
                AssumedBy = new ServicePrincipal("ecs-tasks.amazonaws.com")
            });

            // Add minimum permissions needed (customize based on your app needs)
            TaskRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps
            {
                Effect = Effect.ALLOW,
                Actions = new[] { "s3:GetObject", "s3:ListBucket" },
                Resources = new[] { "arn:aws:s3:::my-app-*" }
            }));

            // Add permissions to access Parameter Store
            TaskRole.AddToPolicy(new PolicyStatement(new PolicyStatementProps
            {
                Effect = Effect.ALLOW,
                Actions = new[] { "ssm:GetParameter", "ssm:GetParameters" },
                Resources = new[] { $"arn:aws:ssm:{region}:*:parameter/myapp/{environmentName}/*" }
            }));

            // Tag resources
            Tags.Of(this).Add("Environment", environmentName);
            Tags.Of(this).Add("Project", "MyApp");
            Tags.Of(this).Add("ResourceType", "Security");
        }
    }
}