using System;
using Amazon.CDK;

namespace MyApp.Pipeline
{
    class Program
    {
        static void Main(string[] args)
        {
            var app = new App();

            new PipelineStack(app, "MyAppPipeline", new StackProps
            {
                Env = new Amazon.CDK.Environment

                {
                    Account = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT"),
                    Region = "us-east-1"
                },
                Description = "CI/CD Pipeline for MyApp multi-region deployment"
            });

            app.Synth();
        }
    }
}