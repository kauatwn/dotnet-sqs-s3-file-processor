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

variable "receive_wait_time_seconds" {
  type        = number
  default     = 20
  description = "The time for which a ReceiveMessage call waits for a message to arrive (long polling, 0-20 seconds)."

  validation {
    condition     = var.receive_wait_time_seconds >= 0 && var.receive_wait_time_seconds <= 20
    error_message = "The receive_wait_time_seconds must be between 0 and 20 seconds."
  }
}

variable "sqs_managed_sse_enabled" {
  type        = bool
  default     = true
  description = "Enable server-side encryption (SSE) using SQS-managed encryption keys (SSE-SQS)."
}

variable "enable_s3_event_policy" {
  type        = bool
  default     = false
  description = "Enable SQS queue policy granting S3 event notification permissions."
}

variable "source_s3_bucket_arn" {
  type        = string
  default     = ""
  description = "ARN of the S3 bucket authorized to publish event notifications (required when enable_s3_event_policy is true)."
}

variable "tags" {
  type        = map(string)
  default     = {}
  description = "Optional map of additional resource tags to be merged with component tags."
}
