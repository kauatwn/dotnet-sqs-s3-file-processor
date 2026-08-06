variable "cluster_name" {
  type        = string
  description = "The name of the Amazon ECS Cluster."

  validation {
    condition     = can(regex("^[a-zA-Z0-9_-]{1,255}$", var.cluster_name))
    error_message = "The cluster_name must contain 1-255 letters, numbers, hyphens, or underscores."
  }
}

variable "environment" {
  type        = string
  description = "Target deployment environment (e.g., dev, staging, prod)."

  validation {
    condition     = contains(["dev", "staging", "prod"], var.environment)
    error_message = "The environment variable must be one of: 'dev', 'staging', 'prod'."
  }
}

variable "vpc_id" {
  type        = string
  description = "The VPC ID where the ECS services will be launched."
}

variable "subnet_ids" {
  type        = list(string)
  description = "List of subnet IDs for ECS task network interface attachment."
}

variable "security_group_ids" {
  type        = list(string)
  description = "List of security group IDs to assign to the ECS task network interfaces."
}

variable "api_task_config" {
  type = object({
    name             = string
    image            = string
    cpu              = number
    memory           = number
    desired_count    = number
    container_port   = number
    environment_vars = map(string)
  })
  description = "Configuration parameters for the Web API Fargate Task Definition and Service."

  validation {
    condition     = contains([256, 512, 1024, 2048, 4096, 8192, 16384], var.api_task_config.cpu)
    error_message = "The api_task_config.cpu must be a valid Fargate CPU value (256, 512, 1024, 2048, 4096, 8192, 16384)."
  }

  validation {
    condition     = var.api_task_config.memory >= 512 && var.api_task_config.memory <= 30720
    error_message = "The api_task_config.memory must be between 512 MB and 30720 MB."
  }

  validation {
    condition     = var.api_task_config.desired_count >= 1
    error_message = "The api_task_config.desired_count must be at least 1."
  }
}

variable "worker_task_config" {
  type = object({
    name             = string
    image            = string
    cpu              = number
    memory           = number
    desired_count    = number
    environment_vars = map(string)
  })
  description = "Configuration parameters for the Background Worker Fargate Task Definition and Service."

  validation {
    condition     = contains([256, 512, 1024, 2048, 4096, 8192, 16384], var.worker_task_config.cpu)
    error_message = "The worker_task_config.cpu must be a valid Fargate CPU value (256, 512, 1024, 2048, 4096, 8192, 16384)."
  }

  validation {
    condition     = var.worker_task_config.memory >= 512 && var.worker_task_config.memory <= 30720
    error_message = "The worker_task_config.memory must be between 512 MB and 30720 MB."
  }

  validation {
    condition     = var.worker_task_config.desired_count >= 1
    error_message = "The worker_task_config.desired_count must be at least 1."
  }
}

variable "log_retention_in_days" {
  type        = number
  default     = 14
  description = "Retention period in days for CloudWatch log groups created for ECS tasks."

  validation {
    condition     = contains([1, 3, 5, 7, 14, 30, 60, 90, 120, 150, 180, 365, 400, 545, 731, 1827, 3653], var.log_retention_in_days)
    error_message = "The log_retention_in_days must be a valid CloudWatch retention period in days."
  }
}

variable "tags" {
  type        = map(string)
  default     = {}
  description = "Optional map of additional resource tags to be merged with component tags."
}
