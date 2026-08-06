variable "db_name" {
  type        = string
  description = "The name of the database to create when the DB instance is created."

  validation {
    condition     = can(regex("^[a-zA-Z][a-zA-Z0-9_]{0,62}$", var.db_name))
    error_message = "The db_name must begin with a letter and contain only alphanumeric characters or underscores (max 63 chars)."
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

variable "instance_class" {
  type        = string
  default     = "db.t4g.micro"
  description = "The compute and memory capacity class of the DB instance."

  validation {
    condition     = can(regex("^db\\.[a-z0-9]+\\.[a-z0-9]+$", var.instance_class))
    error_message = "The instance_class must be a valid RDS instance class (e.g., db.t3.micro, db.t4g.micro)."
  }
}

variable "allocated_storage" {
  type        = number
  default     = 20
  description = "The allocated storage size in gigabytes (GB)."

  validation {
    condition     = var.allocated_storage >= 20 && var.allocated_storage <= 1000
    error_message = "The allocated_storage must be between 20 GB and 1000 GB."
  }
}

variable "engine_version" {
  type        = string
  default     = "16.3"
  description = "The database engine version for PostgreSQL."
}

variable "username" {
  type        = string
  default     = "postgres"
  description = "Master username for the database."

  validation {
    condition     = can(regex("^[a-zA-Z0-9_]{1,32}$", var.username))
    error_message = "The username must contain 1-32 alphanumeric characters or underscores."
  }
}

variable "password" {
  type        = string
  sensitive   = true
  description = "Master password for the database."

  validation {
    condition     = length(var.password) >= 8
    error_message = "The password must be at least 8 characters long."
  }
}

variable "subnet_ids" {
  type        = list(string)
  description = "List of VPC subnet IDs for the RDS DB subnet group."

  validation {
    condition     = length(var.subnet_ids) > 0
    error_message = "At least one subnet ID must be provided."
  }
}

variable "vpc_security_group_ids" {
  type        = list(string)
  description = "List of VPC security group IDs to associate with the DB instance."

  validation {
    condition     = length(var.vpc_security_group_ids) > 0
    error_message = "At least one security group ID must be provided."
  }
}

variable "skip_final_snapshot" {
  type        = bool
  default     = true
  description = "Determines whether a final DB snapshot is created before the DB instance is deleted."
}

variable "tags" {
  type        = map(string)
  default     = {}
  description = "Optional map of additional resource tags to be merged with component tags."
}
