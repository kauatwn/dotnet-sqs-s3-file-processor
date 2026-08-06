output "repository_arn" {
  type        = string
  description = "Full ARN of the ECR repository."
  value       = aws_ecr_repository.this.arn
}

output "repository_url" {
  type        = string
  description = "The URL of the repository (in the form aws_account_id.dkr.ecr.region.amazonaws.com/repositoryName)."
  value       = aws_ecr_repository.this.repository_url
}

output "repository_name" {
  type        = string
  description = "The name of the repository."
  value       = aws_ecr_repository.this.name
}
