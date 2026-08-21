# Dead Letter Queue (DLQ) for failed message handling
resource "aws_sqs_queue" "dlq" {
  name                      = "${var.queue_name}-dlq"
  message_retention_seconds = var.message_retention_seconds * 2 < 1209600 ? var.message_retention_seconds * 2 : 1209600
  sqs_managed_sse_enabled   = var.sqs_managed_sse_enabled

  tags = merge(
    {
      Component   = "Messaging-DLQ"
      Environment = var.environment
      Name        = "${var.queue_name}-dlq"
    },
    var.tags
  )
}

# Redrive allow policy to restrict which source queues can route to this DLQ
resource "aws_sqs_queue_redrive_allow_policy" "dlq" {
  queue_url = aws_sqs_queue.dlq.id

  redrive_allow_policy = jsonencode({
    redrivePermission = "byQueue"
    sourceQueueArns   = [aws_sqs_queue.main.arn]
  })
}

# Main SQS Queue with Redrive Policy pointing to DLQ and Long Polling enabled
resource "aws_sqs_queue" "main" {
  name                       = var.queue_name
  visibility_timeout_seconds = var.visibility_timeout_seconds
  message_retention_seconds  = var.message_retention_seconds
  receive_wait_time_seconds  = var.receive_wait_time_seconds
  sqs_managed_sse_enabled    = var.sqs_managed_sse_enabled

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

# SQS Queue Policy granting S3 event notification permissions with Confused Deputy protection
resource "aws_sqs_queue_policy" "s3_events" {
  count     = var.enable_s3_event_policy ? 1 : 0
  queue_url = aws_sqs_queue.main.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "AllowS3BucketEventNotifications"
        Effect = "Allow"
        Principal = {
          Service = "s3.amazonaws.com"
        }
        Action   = "sqs:SendMessage"
        Resource = aws_sqs_queue.main.arn
        Condition = {
          ArnEquals = {
            "aws:SourceArn" = var.source_s3_bucket_arn
          }
          StringEquals = {
            "aws:SourceAccount" = data.aws_caller_identity.current.account_id
          }
        }
      }
    ]
  })
}

data "aws_caller_identity" "current" {}
