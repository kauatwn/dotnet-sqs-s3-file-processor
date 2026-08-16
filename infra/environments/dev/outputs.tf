output "ecr_api_repository_url" {
  type        = string
  description = "The registry URL of the Web API ECR repository."
  value       = module.ecr_api.repository_url
}

output "ecr_worker_repository_url" {
  type        = string
  description = "The registry URL of the Background Worker ECR repository."
  value       = module.ecr_worker.repository_url
}

output "s3_bucket_name" {
  type        = string
  description = "The unique name of the S3 bucket created for CSV file storage."
  value       = module.s3.bucket_id
}

output "sqs_queue_url" {
  type        = string
  description = "The URL of the primary SQS document processing queue."
  value       = module.sqs.queue_url
}

output "sqs_dlq_url" {
  type        = string
  description = "The URL of the Dead Letter Queue (DLQ)."
  value       = module.sqs.dlq_url
}

output "rds_endpoint" {
  type        = string
  description = "The connection endpoint for the PostgreSQL RDS database."
  value       = module.rds.db_instance_endpoint
}

output "rds_address" {
  type        = string
  description = "The hostname address for the PostgreSQL RDS database."
  value       = module.rds.db_instance_address
}

output "ecs_cluster_arn" {
  type        = string
  description = "ARN of the provisioned ECS cluster."
  value       = module.ecs.cluster_arn
}

output "ecs_api_service_name" {
  type        = string
  description = "Name of the provisioned Web API ECS Service."
  value       = module.ecs.api_service_name
}

output "ecs_worker_service_name" {
  type        = string
  description = "Name of the provisioned Background Worker ECS Service."
  value       = module.ecs.worker_service_name
}
