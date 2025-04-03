# MyApp: Multi-Region AWS CDK Deployment

## 📋 Overview

This project is a multi-region, multi-environment application deployment solution using AWS CDK, designed for robust, scalable, and secure infrastructure management.

## 🛠 Prerequisites

### Local Development Environment
- [.NET 6.0 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)
- [AWS CLI](https://aws.amazon.com/cli/)
- [AWS CDK CLI](https://docs.aws.amazon.com/cdk/latest/guide/cli.html)
- [Docker](https://www.docker.com/products/docker-desktop)
- [Git](https://git-scm.com/downloads)

### AWS Account Setup
- AWS Account with administrative access
- IAM User with programmatic access
- Configured AWS CLI credentials

## 🚀 Initial Setup

### 1. Clone the Repository
```bash
git clone https://github.com/your-username/aws-cdk-multi-region-app.git
cd aws-cdk-multi-region-app
```

### 2. Configure AWS Credentials
```bash
aws configure
# Enter your AWS Access Key ID
# Enter your AWS Secret Access Key
# Enter default region (e.g., us-east-1)
# Enter default output format (json)
```

### 3. Install Global Dependencies
```bash
# Install AWS CDK globally
npm install -g aws-cdk

# Install additional tools
npm install -g cdk-nag
npm install -g snyk
npm install -g artillery
```

### 4. Restore and Build Project
```bash
# Restore .NET dependencies
dotnet restore src/MyApp.Core
dotnet restore src/MyApp.Pipeline

# Build the solution
dotnet build src/MyApp.Core
dotnet build src/MyApp.Pipeline
```

## 🔧 Configuration

### Environment Variables
Set the following environment variables:
```bash
# AWS Account ID
export CDK_DEFAULT_ACCOUNT=your-aws-account-id

# Deployment Regions
export STAGING_REGIONS="us-east-1,us-west-2"
export PRODUCTION_REGIONS="us-east-1,us-west-2"

# GitHub Token (stored in AWS Secrets Manager)
aws secretsmanager create-secret --name github-token --secret-string "YOUR_GITHUB_PERSONAL_ACCESS_TOKEN"
```

### GitHub Repository Configuration
1. Create a GitHub personal access token with repo access
2. Store the token in AWS Secrets Manager with the name `github-token`
3. Update `PipelineStack.cs` with your GitHub repository details

## 🌐 Deployment Stages

### Staging Deployment
```bash
# Deploy to staging environment
cd src/MyApp.Pipeline
dotnet cdk deploy MyApp-Staging
```

### Production Deployment
```bash
# Deploy to production environment
cd src/MyApp.Pipeline
dotnet cdk deploy MyApp-Production
```

## 🔒 Security Scanning

### Pre-Deployment Checks
- `cdk-nag`: Checks for best practices and potential security issues
- `Snyk`: Scans for vulnerabilities in dependencies
- `Prowler`: Conducts AWS security assessments

### Running Security Scans Manually
```bash
# CDK Nag
cdk-nag --template-path cdk.out/*.template.json

# Snyk Scan
snyk test

# Prowler Security Scan
prowler -c check21,check31 -r us-east-1
```

## 🧪 Testing

### Unit Tests
```bash
# Run unit tests
dotnet test test/MyApp.Core.Tests
dotnet test test/MyApp.Environments.Tests
```

### Load Testing
```bash
# Install Artillery
npm install -g artillery

# Run load test
artillery quick --count 10 -n 20 https://your-loadbalancer-dns/health
```

## 🔍 Monitoring and Observability

### CloudWatch Metrics
- Metrics are automatically configured for:
  - ECS Service
  - Application Load Balancer
  - WAF Rules
  - Pipeline Stages

### Parameter Store Tracking
Check deployment details:
```bash
# Last Production Deployment
aws ssm get-parameter --name "/myapp/last-production-deployment"

# Current Image Tag
aws ssm get-parameter --name "/myapp/production-image-tag"
```

## 🛠 Troubleshooting

### Common Issues
- Ensure AWS CLI is configured correctly
- Verify GitHub token in Secrets Manager
- Check IAM permissions
- Validate network configurations

### Rollback Strategies
- Automatic rollback configured in pipeline
- Manual rollback via AWS Console or CLI

## 📋 Project Structure
```
aws-cdk-multi-region-app/
│
├── src/
│   ├── MyApp.Core/          # Core application logic
│   ├── MyApp.Pipeline/      # CDK Pipeline configuration
│   └── MyApp.Environments/  # Environment-specific configurations
│
├── test/                    # Test projects
│   ├── MyApp.Core.Tests/
│   └── MyApp.Environments.Tests/
│
└── README.md
```

## 🤝 Contributing
1. Fork the repository
2. Create a feature branch
3. Commit changes
4. Push to the branch
5. Create a Pull Request

## 📄 License
[Specify your license here]

## 🆘 Support
For issues or questions, please [open a GitHub issue](https://github.com/your-username/aws-cdk-multi-region-app/issues)