output "queue_url" {
  type        = string
  description = "The URL of the primary SQS queue."
  value       = aws_sqs_queue.main.url
}

output "queue_arn" {
  type        = string
  description = "The ARN of the primary SQS queue."
  value       = aws_sqs_queue.main.arn
}

output "queue_name" {
  type        = string
  description = "The name of the primary SQS queue."
  value       = aws_sqs_queue.main.name
}

output "dlq_url" {
  type        = string
  description = "The URL of the Dead Letter Queue (DLQ)."
  value       = aws_sqs_queue.dlq.url
}

output "dlq_arn" {
  type        = string
  description = "The ARN of the Dead Letter Queue (DLQ)."
  value       = aws_sqs_queue.dlq.arn
}
