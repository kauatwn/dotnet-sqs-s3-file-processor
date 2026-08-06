output "cluster_id" {
  type        = string
  description = "The ID of the ECS cluster."
  value       = aws_ecs_cluster.this.id
}

output "cluster_arn" {
  type        = string
  description = "The ARN of the ECS cluster."
  value       = aws_ecs_cluster.this.arn
}

output "api_service_name" {
  type        = string
  description = "The name of the Web API ECS Service."
  value       = aws_ecs_service.api.name
}

output "worker_service_name" {
  type        = string
  description = "The name of the Background Worker ECS Service."
  value       = aws_ecs_service.worker.name
}

output "api_task_definition_arn" {
  type        = string
  description = "The ARN of the Web API ECS Task Definition."
  value       = aws_ecs_task_definition.api.arn
}

output "worker_task_definition_arn" {
  type        = string
  description = "The ARN of the Background Worker ECS Task Definition."
  value       = aws_ecs_task_definition.worker.arn
}

output "execution_role_arn" {
  type        = string
  description = "The ARN of the ECS Task Execution Role."
  value       = aws_iam_role.execution_role.arn
}

output "task_role_arn" {
  type        = string
  description = "The ARN of the ECS Task Role."
  value       = aws_iam_role.task_role.arn
}
