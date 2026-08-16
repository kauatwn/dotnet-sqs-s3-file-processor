variable "queue_name" {
  type        = string
  description = "Name of the primary Amazon SQS queue."

  validation {
    condition     = can(regex("^[a-zA-Z0-9_-]{1,80}$", var.queue_name))
    error_message = "The queue_name must contain only 1-80 letters, numbers, hyphens, or underscores."
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

variable "max_receive_count" {
  type        = number
  default     = 5
  description = "Maximum number of times a message can be received before being sent to the DLQ."

  validation {
    condition     = var.max_receive_count >= 1 && var.max_receive_count <= 100
    error_message = "The max_receive_count must be between 1 and 100."
  }
}

variable "message_retention_seconds" {
  type        = number
  default     = 345600 # 4 days
  description = "The number of seconds Amazon SQS retains a message."

  validation {
    condition     = var.message_retention_seconds >= 60 && var.message_retention_seconds <= 1209600
    error_message = "The message_retention_seconds must be between 60 and 1209600 (14 days)."
  }
}

variable "visibility_timeout_seconds" {
  type        = number
  default     = 30
  description = "The visibility timeout for the queue, in seconds."

  validation {
    condition     = var.visibility_timeout_seconds >= 0 && var.visibility_timeout_seconds <= 43200
    error_message = "The visibility_timeout_seconds must be between 0 and 43200 (12 hours)."
  }
}

variable "tags" {
  type        = map(string)
  default     = {}
  description = "Optional map of additional resource tags to be merged with component tags."
}
