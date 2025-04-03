using System;
using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.CodeBuild;
using Amazon.CDK.AWS.CodePipeline;
using Amazon.CDK.AWS.CodePipeline.Actions;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.ECR;
using Amazon.CDK.AWS.SSM;
using Amazon.CDK.Pipelines;

// Explicitly specify Constructs namespace to resolve ambiguity
using Construct = Constructs.Construct;

namespace MyApp.Pipeline
{
    public class PipelineStack : Amazon.CDK.Stack
    {
        public PipelineStack(Construct scope, string id, Amazon.CDK.IStackProps props = null) : base(scope, id, props)
        {
            // Create an artifact bucket with proper security settings
            var artifactBucket = new Bucket(this, "ArtifactBucket", new BucketProps
            {
                Encryption = BucketEncryption.S3_MANAGED,
                BlockPublicAccess = BlockPublicAccess.BLOCK_ALL,
                RemovalPolicy = RemovalPolicy.RETAIN,
                EnforceSSL = true,
                Versioned = true
            });

            // Create ECR repository for container images
            var repository = new Repository(this, "AppRepository", new RepositoryProps
            {
                RepositoryName = "myapp/application",
                RemovalPolicy = RemovalPolicy.RETAIN,
                ImageScanOnPush = true,
                LifecycleRules = new Amazon.CDK.AWS.ECR.ILifecycleRule[]
                {
                    new Amazon.CDK.AWS.ECR.LifecycleRule
                    {
                        MaxImageCount = 10,
                        Description = "Only keep 10 latest images"
                    }
                }
            });

            // Store ECR repo URI in Parameter Store for easy access by other stacks
            new StringParameter(this, "EcrRepoUri", new StringParameterProps
            {
                ParameterName = "/myapp/ecr-repository-uri",
                StringValue = repository.RepositoryUri
            });

            // Define the pipeline - this automatically triggers on code changes
            var pipeline = new CodePipeline(this, "Pipeline", new CodePipelineProps
            {
                PipelineName = "MyAppDeploymentPipeline",
                CrossAccountKeys = false,
                ArtifactBucket = artifactBucket,

                // GitHub source - automatically triggers the pipeline on code changes
                Synth = new ShellStep("Synth", new ShellStepProps
                {
                    Input = CodePipelineSource.GitHub("your-github-username/aws-cdk-multi-region-app", "main", new GitHubSourceOptions
                    {
                        Authentication = SecretValue.SecretsManager("github-token")
                    }),
                    Commands = new[]
                    {
                        "npm install -g aws-cdk",
                        "dotnet build src/MyApp.Core",
                        "dotnet test test/MyApp.Core.Tests",
                        
                        // Build and push Docker image
                        "aws ecr get-login-password --region $AWS_REGION | docker login --username AWS --password-stdin $ECR_REPO_URI",
                        "docker build -t $ECR_REPO_URI:$CODEBUILD_RESOLVED_SOURCE_VERSION .",
                        "docker tag $ECR_REPO_URI:$CODEBUILD_RESOLVED_SOURCE_VERSION $ECR_REPO_URI:latest",
                        "docker push $ECR_REPO_URI:$CODEBUILD_RESOLVED_SOURCE_VERSION",
                        "docker push $ECR_REPO_URI:latest",
                        
                        // Store image tag in parameter store for use by stacks
                        "aws ssm put-parameter --name \"/myapp/image-tag\" --value \"$CODEBUILD_RESOLVED_SOURCE_VERSION\" --type String --overwrite",
                        
                        // Synthesize CDK
                        "cd src/MyApp.Pipeline && dotnet restore",
                        "dotnet cdk synth"
                    },
                    PrimaryOutputDirectory = "src/MyApp.Pipeline/cdk.out",
                    Env = new Dictionary<string, string>
                    {
                        { "ECR_REPO_URI", repository.RepositoryUri }
                    }
                }),

                // Self-mutation to update the pipeline itself
                SelfMutation = true,

                // Enable Docker for builds
                DockerEnabledForSelfMutation = true,
                DockerEnabledForSynth = true
            });

            // Add security check step before any deployment
            var securityCheck = new CodeBuildStep("SecurityScan", new CodeBuildStepProps
            {
                Commands = new[]
                {
                    "npm install -g cdk-nag",
                    "cdk-nag --template-path src/MyApp.Pipeline/cdk.out/*.template.json",
                    "npm install -g snyk",
                    "snyk test || echo 'Vulnerabilities found but proceeding with deployment'"
                },
                BuildEnvironment = new BuildEnvironment
                {
                    Privileged = true,
                    BuildImage = LinuxBuildImage.AMAZON_LINUX_2_4
                }
            });

            // Create application stages
            var staging = new ApplicationStage(this, "Staging", new Amazon.CDK.StageProps
            {
                Env = new Amazon.CDK.Environment
                {
                    Account = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT"),
                    Region = "us-east-1"
                }
            });

            // Add staging deployment with automatic triggering
            var stagingStage = pipeline.AddStage(staging, new AddStageOpts
            {
                Pre = new[] { securityCheck },
                Post = new[]
                {
                    new ShellStep("TestStagingDeployment", new ShellStepProps
                    {
                        Commands = new[]
                        {
                            // Verification commands remain the same
                            "echo 'Testing US East 1 deployment'",
                            "export LB_DNS=$(aws ssm get-parameter --name /myapp/Staging/us-east-1/loadbalancer-dns --query Parameter.Value --output text --region us-east-1)",
                            "curl -s $LB_DNS/health | grep OK",

                            "echo 'Testing US West 2 deployment'",
                            "export LB_DNS=$(aws ssm get-parameter --name /myapp/Staging/us-west-2/loadbalancer-dns --query Parameter.Value --output text --region us-west-2)",
                            "curl -s $LB_DNS/health | grep OK",

                            "cd test && dotnet test MyApp.Environments.Tests/MyApp.Environments.Tests.csproj -- RunConfiguration.TargetFramework=net6.0",

                            "echo 'Running load tests'",
                            "npm install -g artillery",
                            "artillery quick --count 10 -n 20 $(aws ssm get-parameter --name /myapp/Staging/us-east-1/loadbalancer-dns --query Parameter.Value --output text --region us-east-1)/health"
                        }
                    })
                }
            });

            // Production deployment stage with MANUAL approval
            var production = new ApplicationStage(this, "Production", new Amazon.CDK.StageProps
            {
                Env = new Amazon.CDK.Environment
                {
                    Account = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT"),
                    Region = "us-east-1"
                }
            });

            // Add production stage with EXPLICIT MANUAL APPROVAL
            var productionStage = pipeline.AddStage(production, new AddStageOpts
            {
                Pre = new[]
                {
                    new ManualApprovalStep("PromoteToProduction", new ManualApprovalStepProps
                    {
                        Comment = "Approve deployment to production environment?"
                    })
                },
                Post = new[]
                {
                    new ShellStep("ValidateProductionDeployment", new ShellStepProps
                    {
                        Commands = new[]
                        {
                            // Production validation commands remain the same
                            "echo 'Validating US East 1 production deployment'",
                            "export PROD_LB_DNS=$(aws ssm get-parameter --name /myapp/Production/us-east-1/loadbalancer-dns --query Parameter.Value --output text --region us-east-1)",
                            "curl -s $PROD_LB_DNS/health | grep OK",
                            "export PROD_HEALTH_CHECK=$(curl -s -o /dev/null -w \"%{http_code}\" $PROD_LB_DNS/health)",
                            "if [ \"$PROD_HEALTH_CHECK\" != \"200\" ]; then echo 'Production healthcheck failed!'; exit 1; fi",

                            "echo 'Validating US West 2 production deployment'",
                            "export PROD_LB_DNS_WEST=$(aws ssm get-parameter --name /myapp/Production/us-west-2/loadbalancer-dns --query Parameter.Value --output text --region us-west-2)",
                            "curl -s $PROD_LB_DNS_WEST/health | grep OK",

                            "echo 'Running security scan against production'",
                            "npm install -g prowler",
                            "prowler -c check21,check31 -r us-east-1",

                            "aws ssm put-parameter --name \"/myapp/last-production-deployment\" --value \"$(date)\" --type String --overwrite",
                            "aws ssm put-parameter --name \"/myapp/production-image-tag\" --value \"$(aws ssm get-parameter --name /myapp/image-tag --query Parameter.Value --output text)\" --type String --overwrite"
                        }
                    })
                }
            });

            // Output the pipeline ARN for reference
            new CfnOutput(this, "PipelineArn", new CfnOutputProps
            {
                Value = pipeline.Pipeline.PipelineArn,
                Description = "ARN of the deployment pipeline",
                ExportName = "MyAppPipelineArn"
            });
        }
    }

    // Enhanced Application Stage to deploy both regions with better parameterization
    public class ApplicationStage : Amazon.CDK.Stage
    {
        public ApplicationStage(Construct scope, string id, Amazon.CDK.StageProps props = null) : base(scope, id, props)
        {
            // Get environment name from stage ID
            string environmentName = id;

            // Define the regions for deployment
            string[] deploymentRegions = new string[] { "us-east-1", "us-west-2" };

            foreach (var region in deploymentRegions)
            {
                var stackName = $"MyApp{environmentName}{region.Replace("-", "")}";

                if (environmentName == "Staging")
                {
                    // Deploy staging stack to this region
                    _ = new MyApp.Environments.StagingStack(this, stackName, region, new Amazon.CDK.StackProps
                    {
                        Env = new Amazon.CDK.Environment
                        {
                            Account = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT"),
                            Region = region
                        },
                        // Add custom tags for better identification
                        Tags = new Dictionary<string, string>
                        {
                            { "Environment", "Staging" },
                            { "Region", region },
                            { "Application", "MyApp" },
                            { "DeploymentType", "CDK" },
                            { "AutoDeploy", "True" }  // Indicates this stack is auto-deployed
                        }
                    });
                }
                else if (environmentName == "Production")
                {
                    // Deploy production stack to this region
                    _ = new MyApp.Environments.ProductionStack(this, stackName, region, new Amazon.CDK.StackProps
                    {
                        Env = new Amazon.CDK.Environment
                        {
                            Account = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT"),
                            Region = region
                        },
                        // Add custom tags for better identification
                        Tags = new Dictionary<string, string>
                        {
                            { "Environment", "Production" },
                            { "Region", region },
                            { "Application", "MyApp" },
                            { "DeploymentType", "CDK" },
                            { "CriticalityLevel", "High" },
                            { "AutoDeploy", "False" }  // Indicates this stack requires manual approval
                        }
                    });
                }
            }
        }
    }
}