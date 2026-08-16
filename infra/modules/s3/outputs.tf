output "bucket_id" {
  type        = string
  description = "The name (ID) of the S3 bucket."
  value       = aws_s3_bucket.this.id
}

output "bucket_arn" {
  type        = string
  description = "The ARN of the S3 bucket."
  value       = aws_s3_bucket.this.arn
}

output "bucket_domain_name" {
  type        = string
  description = "The bucket domain name."
  value       = aws_s3_bucket.this.bucket_domain_name
}
