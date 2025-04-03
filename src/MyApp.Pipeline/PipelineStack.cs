using System;
using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.CodeBuild;
using Amazon.CDK.AWS.CodeCommit;
using Amazon.CDK.AWS.CodePipeline;
using Amazon.CDK.AWS.CodePipeline.Actions;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.ECR;
using Amazon.CDK.AWS.SSM;
using Amazon.CDK.Pipelines;
using Constructs;

namespace MyApp.Pipeline;

public class PipelineStack : Stack
{
    public PipelineStack(Construct scope, string id, IStackProps? props = null) : base(scope, id, props)
    {
        // Create the pipeline
        var pipeline = new CodePipeline(this, "Pipeline", new CodePipelineProps
        {
            PipelineName = "MyAppPipeline",
            SelfMutation = true,
            DockerEnabledForSelfMutation = true,
            Synth = new ShellStep("Synth", new ShellStepProps
            {
                Input = CodePipelineSource.GitHub(
                    "haripal143/aws-cdk-deploy",
                    "main",
                    new GitHubSourceOptions
                    {
                        Authentication = SecretValue.SecretsManager("github-token")
                    }
                ),
                Commands = new[]
                {
                    "npm install -g aws-cdk",
                    "dotnet restore",
                    "dotnet build",
                    "cdk synth"
                }
            })
        });

        // Create ECR repository
        var repository = new Amazon.CDK.AWS.ECR.Repository(this, "MyAppRepository", new Amazon.CDK.AWS.ECR.RepositoryProps
        {
            RepositoryName = "my-app-repository",
            RemovalPolicy = RemovalPolicy.DESTROY,
            LifecycleRules = new Amazon.CDK.AWS.ECR.ILifecycleRule[]
            {
                new Amazon.CDK.AWS.ECR.LifecycleRule
                {
                    MaxImageCount = 3,
                    Description = "Keep only the last 3 images"
                }
            }
        });

        // Create S3 bucket for artifacts
        var artifactBucket = new Bucket(this, "ArtifactBucket", new BucketProps
        {
            RemovalPolicy = RemovalPolicy.DESTROY,
            AutoDeleteObjects = true,
            LifecycleRules = new Amazon.CDK.AWS.S3.ILifecycleRule[]
            {
                new Amazon.CDK.AWS.S3.LifecycleRule
                {
                    Expiration = Duration.Days(30)
                }
            }
        });

        // Create IAM role for CodeBuild
        var buildRole = new Role(this, "BuildRole", new RoleProps
        {
            AssumedBy = new ServicePrincipal("codebuild.amazonaws.com"),
            ManagedPolicies = new IManagedPolicy[]
            {
                ManagedPolicy.FromAwsManagedPolicyName("AmazonS3ReadOnlyAccess"),
                ManagedPolicy.FromAwsManagedPolicyName("AmazonEC2ContainerRegistryFullAccess")
            }
        });

        // Create CodeBuild project
        var buildProject = new Project(this, "BuildProject", new ProjectProps
        {
            Environment = new BuildEnvironment
            {
                BuildImage = LinuxBuildImage.AMAZON_LINUX_2_4,
                Privileged = true
            },
            Source = Source.GitHub(new GitHubSourceProps
            {
                Owner = "haripal143",
                Repo = "aws-cdk-deploy",
                // We won't set up webhooks for the build project, as the pipeline handles that
                Webhook = false
            }),
            Artifacts = Artifacts.S3(new S3ArtifactsProps
            {
                Bucket = artifactBucket,
                Name = "build-output"
            }),
            Role = buildRole
        });

        // Add stages to the pipeline
        pipeline.AddStage(new ApplicationStage(this, "Staging", new Amazon.CDK.StageProps
        {
            Env = new Amazon.CDK.Environment
            {
                Account = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT"),
                Region = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_REGION")
            }
        }));

        // Store ECR repo URI in Parameter Store for easy access by other stacks
        new StringParameter(this, "EcrRepoUri", new StringParameterProps
        {
            ParameterName = "/myapp/ecr-repository-uri",
            StringValue = repository.RepositoryUri
        });

        // Output the pipeline ARN for reference if available
        try
        {
            new CfnOutput(this, "PipelineArn", new CfnOutputProps
            {
                Value = pipeline.Pipeline?.PipelineArn ?? "Pipeline not fully created yet",
                Description = "ARN of the deployment pipeline",
                ExportName = "MyAppPipelineArn"
            });
        }
        catch
        {
            // Pipeline may not be fully created yet, output a placeholder
            new CfnOutput(this, "PipelineArnPlaceholder", new CfnOutputProps
            {
                Value = "Pipeline not fully created yet",
                Description = "ARN of the deployment pipeline will be available after deployment",
                ExportName = "MyAppPipelineArnPlaceholder"
            });
        }
    }
}

public class MyAppStage : Stage
{
    public MyAppStage(Construct scope, string id, Amazon.CDK.StageProps? props = null) : base(scope, id, props)
    {
        // Add your application stack here
        // new MyAppStack(this, "MyAppStack");
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
            else
            {
                // Default to staging for any other environment name to ensure we always create at least one stack
                _ = new MyApp.Environments.StagingStack(this, stackName, region, new Amazon.CDK.StackProps
                {
                    Env = new Amazon.CDK.Environment
                    {
                        Account = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT"),
                        Region = region
                    },
                    Tags = new Dictionary<string, string>
                    {
                        { "Environment", environmentName },
                        { "Region", region },
                        { "Application", "MyApp" },
                        { "DeploymentType", "CDK" },
                        { "AutoDeploy", "True" }
                    }
                });
            }
        }
    }
}