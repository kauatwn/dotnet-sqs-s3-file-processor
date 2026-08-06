locals {
  common_tags = {
    Environment = var.environment
    ManagedBy   = "Terraform"
    Project     = var.project_name
  }

  api_repo_name    = "${var.project_name}-api-${var.environment}"
  worker_repo_name = "${var.project_name}-worker-${var.environment}"
  cluster_name     = "${var.project_name}-cluster-${var.environment}"
  bucket_name      = "${var.project_name}-files-${var.environment}"
  queue_name       = "${var.project_name}-document-processing-${var.environment}"
  db_name          = "${var.project_name}_db"

  effective_security_group_ids = length(var.security_group_ids) > 0 && var.security_group_ids[0] != "sg-default" ? var.security_group_ids : [data.aws_security_group.default.id]
}

# Dynamic Network Resolution (Default VPC & Subnets & Security Group)
data "aws_vpc" "default" {
  default = true
}

data "aws_subnets" "default" {
  filter {
    name   = "vpc-id"
    values = [data.aws_vpc.default.id]
  }
}

data "aws_security_group" "default" {
  name   = "default"
  vpc_id = data.aws_vpc.default.id
}

# 1. Pure Module: ECR Repository for Web API
module "ecr_api" {
  source = "../../modules/ecr"

  repository_name      = local.api_repo_name
  image_tag_mutability = "MUTABLE"
  scan_on_push         = true
  environment          = var.environment
  tags                 = local.common_tags
}

# 2. Pure Module: ECR Repository for Background Worker
module "ecr_worker" {
  source = "../../modules/ecr"

  repository_name      = local.worker_repo_name
  image_tag_mutability = "MUTABLE"
  scan_on_push         = true
  environment          = var.environment
  tags                 = local.common_tags
}

# 3. Pure Module: S3 Storage for CSV files
module "storage" {
  source = "../../modules/storage"

  bucket_name = local.bucket_name
  environment = var.environment
  tags        = local.common_tags
}

# 4. Pure Module: SQS Queue & Dead Letter Queue (DLQ)
module "messaging" {
  source = "../../modules/messaging"

  queue_name        = local.queue_name
  environment       = var.environment
  max_receive_count = 5
  tags              = local.common_tags
}

# 5. Pure Module: RDS PostgreSQL Database
module "database" {
  source = "../../modules/database"

  db_name                = local.db_name
  environment            = var.environment
  instance_class         = var.db_instance_class
  allocated_storage      = 20
  username               = var.db_username
  password               = var.db_password
  subnet_ids             = data.aws_subnets.default.ids
  vpc_security_group_ids = local.effective_security_group_ids
  skip_final_snapshot    = true
  tags                   = local.common_tags
}

# 6. Pure Module: ECS Cluster & Fargate Services (Glue Code dynamically receiving outputs from ECR, Messaging, Database & Storage)
module "ecs" {
  source = "../../modules/ecs"

  cluster_name       = local.cluster_name
  environment        = var.environment
  vpc_id             = data.aws_vpc.default.id
  subnet_ids         = data.aws_subnets.default.ids
  security_group_ids = local.effective_security_group_ids

  api_task_config = {
    name           = "api"
    image          = "${module.ecr_api.repository_url}:latest"
    cpu            = var.api_task_config.cpu
    memory         = var.api_task_config.memory
    desired_count  = var.api_task_config.desired_count
    container_port = var.api_task_config.container_port
    environment_vars = {
      "ASPNETCORE_ENVIRONMENT"               = "Production"
      "ASPNETCORE_HTTP_PORTS"                = tostring(var.api_task_config.container_port)
      "ConnectionStrings__DefaultConnection" = module.database.connection_string
      "AWS__SQS__QueueUrl"                   = module.messaging.queue_url
      "AWS__S3__BucketName"                  = module.storage.bucket_id
    }
  }

  worker_task_config = {
    name          = "worker"
    image         = "${module.ecr_worker.repository_url}:latest"
    cpu           = var.worker_task_config.cpu
    memory        = var.worker_task_config.memory
    desired_count = var.worker_task_config.desired_count
    environment_vars = {
      "DOTNET_ENVIRONMENT"                   = "Production"
      "ConnectionStrings__DefaultConnection" = module.database.connection_string
      "AWS__SQS__QueueUrl"                   = module.messaging.queue_url
      "AWS__S3__BucketName"                  = module.storage.bucket_id
    }
  }

  tags = local.common_tags
}
