using Amazon.CDK;
using System;

namespace MyApp.Production
{
    class Program
    {
        static void Main(string[] args)
        {
            var app = new App();

            // Deploy to both regions
            new MyApp.Environments.ProductionStack(app, "MyAppProductionUsEast1", "us-east-1", new StackProps
            {
                Env = new Amazon.CDK.Environment
                {
                    Account = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT") ?? "123456789012",
                    Region = "us-east-1"
                },
                Description = "MyApp Production Environment (US East 1)"
            });

            new MyApp.Environments.ProductionStack(app, "MyAppProductionUsWest2", "us-west-2", new StackProps
            {
                Env = new Amazon.CDK.Environment
                {
                    Account = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT") ?? "123456789012",
                    Region = "us-west-2"
                },
                Description = "MyApp Production Environment (US West 2)"
            });

            app.Synth();
        }
    }
}