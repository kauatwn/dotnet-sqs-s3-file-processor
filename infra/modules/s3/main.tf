resource "aws_s3_bucket" "this" {
  bucket        = var.bucket_name
  force_destroy = var.environment == "dev" ? true : false

  tags = merge(
    {
      Component   = "Storage"
      Environment = var.environment
      Name        = var.bucket_name
    },
    var.tags
  )
}

# Block all public access for security compliance
resource "aws_s3_bucket_public_access_block" "this" {
  bucket = aws_s3_bucket.this.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

# Enable Server-Side Encryption by default
resource "aws_s3_bucket_server_side_encryption_configuration" "this" {
  bucket = aws_s3_bucket.this.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
  }
}

# S3 Event Notification to SQS Queue (Core functional architecture)
resource "aws_s3_bucket_notification" "sqs_notification" {
  count  = var.sqs_queue_arn != null ? 1 : 0
  bucket = aws_s3_bucket.this.id

  queue {
    queue_arn     = var.sqs_queue_arn
    events        = ["s3:ObjectCreated:*"]
    filter_suffix = ".csv"
  }
}
