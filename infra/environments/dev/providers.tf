terraform {
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.55.0"
    }
  }
}

provider "aws" {
  access_key = "test"
  secret_key = "test"
  region     = var.aws_region

  # Global tags applied automatically to all supported resources
  default_tags {
    tags = {
      Environment = var.environment
      Project     = var.project_name
      ManagedBy   = "Terraform"
    }
  }

  # Only required for non-virtual hosted-style endpoint use case
  s3_use_path_style           = true
  skip_credentials_validation = true
  skip_metadata_api_check     = true
  skip_requesting_account_id  = true

  endpoints {
    s3         = var.localstack_endpoint
    sqs        = var.localstack_endpoint
    ec2        = var.localstack_endpoint
    ecr        = var.localstack_endpoint
    ecs        = var.localstack_endpoint
    rds        = var.localstack_endpoint
    iam        = var.localstack_endpoint
    sts        = var.localstack_endpoint
    logs       = var.localstack_endpoint
    cloudwatch = var.localstack_endpoint
  }
}
