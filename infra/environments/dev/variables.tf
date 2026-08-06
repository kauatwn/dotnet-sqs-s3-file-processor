variable "aws_region" {
  type        = string
  default     = "us-east-1"
  description = "AWS region for deployment."

  validation {
    condition     = can(regex("^[a-z]{2}-(?:gov-)?(?:east|west|north|south|central|northeast|southeast|southwest)-[1-4]$", var.aws_region))
    error_message = "The aws_region must be a valid AWS region identifier (e.g., us-east-1, us-west-2, eu-west-1)."
  }
}

variable "environment" {
  type        = string
  default     = "dev"
  description = "Target deployment environment identifier (e.g., dev, staging, prod)."

  validation {
    condition     = contains(["dev", "staging", "prod"], var.environment)
    error_message = "The environment variable must be one of: 'dev', 'staging', 'prod'."
  }
}

variable "project_name" {
  type        = string
  default     = "fileprocessor"
  description = "Short identifier for the project used in resource naming and tags."

  validation {
    condition     = can(regex("^[a-z0-9-]{3,30}$", var.project_name))
    error_message = "The project_name must be between 3 and 30 lowercase alphanumeric characters or hyphens."
  }
}

variable "localstack_endpoint" {
  type        = string
  default     = "http://localhost:4566"
  description = "LocalStack endpoint URL for local AWS emulation."

  validation {
    condition     = can(regex("^https?://", var.localstack_endpoint))
    error_message = "The localstack_endpoint must be a valid HTTP or HTTPS URL."
  }
}

variable "security_group_ids" {
  type        = list(string)
  default     = []
  description = "List of security group IDs for network interfaces. If empty or dummy placeholder 'sg-default', the default VPC security group is dynamically resolved."
}

variable "db_username" {
  type        = string
  default     = "postgres"
  description = "Master username for PostgreSQL RDS database."

  validation {
    condition     = can(regex("^[a-zA-Z0-9_]{1,32}$", var.db_username))
    error_message = "The db_username must contain 1-32 alphanumeric characters or underscores."
  }
}

variable "db_password" {
  type        = string
  default     = "ChangeMe123!"
  sensitive   = true
  description = "Master password for PostgreSQL RDS database."

  validation {
    condition     = length(var.db_password) >= 8
    error_message = "The db_password must be at least 8 characters long."
  }
}

variable "db_instance_class" {
  type        = string
  default     = "db.t4g.micro"
  description = "Compute instance class for PostgreSQL RDS database."

  validation {
    condition     = can(regex("^db\\.[a-z0-9]+\\.[a-z0-9]+$", var.db_instance_class))
    error_message = "The db_instance_class must be a valid RDS instance class identifier."
  }
}

variable "api_task_config" {
  type = object({
    cpu            = number
    memory         = number
    desired_count  = number
    container_port = number
  })
  default = {
    cpu            = 256
    memory         = 512
    desired_count  = 1
    container_port = 8080
  }
  description = "Fargate resource sizing and scaling configuration for Web API."

  validation {
    condition     = contains([256, 512, 1024, 2048, 4096, 8192, 16384], var.api_task_config.cpu)
    error_message = "The api_task_config.cpu must be a valid Fargate CPU value."
  }
}

variable "worker_task_config" {
  type = object({
    cpu           = number
    memory        = number
    desired_count = number
  })
  default = {
    cpu           = 256
    memory        = 512
    desired_count = 1
  }
  description = "Fargate resource sizing and scaling configuration for Background Worker."

  validation {
    condition     = contains([256, 512, 1024, 2048, 4096, 8192, 16384], var.worker_task_config.cpu)
    error_message = "The worker_task_config.cpu must be a valid Fargate CPU value."
  }
}
