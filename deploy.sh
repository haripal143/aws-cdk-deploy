#!/bin/bash
# Simple deployment script for the AWS CDK project

set -e

# Check prerequisites
command -v dotnet >/dev/null 2>&1 || { echo "dotnet is required but not installed. Aborting." >&2; exit 1; }
command -v aws >/dev/null 2>&1 || { echo "AWS CLI is required but not installed. Aborting." >&2; exit 1; }
command -v npm >/dev/null 2>&1 || { echo "npm is required but not installed. Aborting." >&2; exit 1; }

# Install AWS CDK if needed
if ! command -v cdk >/dev/null 2>&1; then
    echo "Installing AWS CDK..."
    npm install -g aws-cdk
fi

# Restore dependencies
echo "Restoring .NET dependencies..."
dotnet restore MyApp.sln

# Build the solution
echo "Building solution..."
dotnet build MyApp.sln

# Bootstrap CDK environment if needed
echo "Bootstrapping CDK environment..."
cdk bootstrap

# Deploy pipeline
echo "Deploying CI/CD pipeline..."
cd src/MyApp.Pipeline
dotnet build
cdk deploy --require-approval never

echo "Deployment complete!"
echo "Pipeline has been deployed. It will automatically deploy to Staging environments."
echo "Production deployment requires manual approval in AWS CodePipeline console."