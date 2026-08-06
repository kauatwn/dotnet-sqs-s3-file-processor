# Dead Letter Queue (DLQ) for failed message handling
resource "aws_sqs_queue" "dlq" {
  name                      = "${var.queue_name}-dlq"
  message_retention_seconds = var.message_retention_seconds * 2 < 1209600 ? var.message_retention_seconds * 2 : 1209600

  tags = merge(
    {
      Component   = "Messaging-DLQ"
      Environment = var.environment
      Name        = "${var.queue_name}-dlq"
    },
    var.tags
  )
}

# Main SQS Queue with Redrive Policy pointing to DLQ
resource "aws_sqs_queue" "main" {
  name                       = var.queue_name
  visibility_timeout_seconds = var.visibility_timeout_seconds
  message_retention_seconds  = var.message_retention_seconds

  redrive_policy = jsonencode({
    deadLetterTargetArn = aws_sqs_queue.dlq.arn
    maxReceiveCount     = var.max_receive_count
  })

  tags = merge(
    {
      Component   = "Messaging-MainQueue"
      Environment = var.environment
      Name        = var.queue_name
    },
    var.tags
  )
}
