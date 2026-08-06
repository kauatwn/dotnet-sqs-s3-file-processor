variable "repository_name" {
  type        = string
  description = "The name of the Amazon ECR repository."

  validation {
    condition     = can(regex("^[a-z0-9][a-z0-9._/-]{1,254}$", var.repository_name))
    error_message = "The repository_name must be a valid ECR repository name (lowercase alphanumeric characters, hyphens, underscores, periods, and forward slashes)."
  }
}

variable "image_tag_mutability" {
  type        = string
  default     = "MUTABLE"
  description = "The tag mutability setting for the repository (MUTABLE or IMMUTABLE)."

  validation {
    condition     = contains(["MUTABLE", "IMMUTABLE"], var.image_tag_mutability)
    error_message = "The image_tag_mutability must be either 'MUTABLE' or 'IMMUTABLE'."
  }
}

variable "scan_on_push" {
  type        = bool
  default     = true
  description = "Indicates whether images are scanned after being pushed to the repository."
}

variable "environment" {
  type        = string
  description = "Target deployment environment (e.g., dev, staging, prod)."

  validation {
    condition     = contains(["dev", "staging", "prod"], var.environment)
    error_message = "The environment variable must be one of: 'dev', 'staging', 'prod'."
  }
}

variable "tags" {
  type        = map(string)
  default     = {}
  description = "Optional map of additional resource tags to be merged with component tags."
}
