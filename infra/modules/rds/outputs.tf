output "db_instance_id" {
  type        = string
  description = "The ID of the RDS database instance."
  value       = aws_db_instance.this.id
}

output "db_instance_arn" {
  type        = string
  description = "The ARN of the RDS database instance."
  value       = aws_db_instance.this.arn
}

output "db_instance_endpoint" {
  type        = string
  description = "The connection endpoint in the format host:port."
  value       = aws_db_instance.this.endpoint
}

output "db_instance_address" {
  type        = string
  description = "The hostname of the RDS database instance."
  value       = aws_db_instance.this.address
}

output "db_instance_port" {
  type        = number
  description = "The database port."
  value       = aws_db_instance.this.port
}

output "db_name" {
  type        = string
  description = "The name of the default database created."
  value       = aws_db_instance.this.db_name
}

output "db_username" {
  type        = string
  description = "The master username for the database."
  value       = aws_db_instance.this.username
}

output "connection_string" {
  type        = string
  sensitive   = true
  description = "Formatted PostgreSQL connection string for .NET Core applications."
  value       = "Host=${aws_db_instance.this.address};Port=${aws_db_instance.this.port};Database=${aws_db_instance.this.db_name};Username=${aws_db_instance.this.username};Password=${aws_db_instance.this.password}"
}
