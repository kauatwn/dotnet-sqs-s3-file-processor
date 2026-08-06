# DB Subnet Group for RDS Instance placement
resource "aws_db_subnet_group" "this" {
  name        = replace(lower("${var.db_name}-${var.environment}-subnet-group"), "_", "-")
  subnet_ids  = var.subnet_ids
  description = "Database subnet group for ${var.db_name} (${var.environment})"

  tags = merge(
    {
      Component   = "Database-SubnetGroup"
      Environment = var.environment
    },
    var.tags
  )
}

# Amazon RDS PostgreSQL DB Instance
resource "aws_db_instance" "this" {
  identifier             = replace(lower("${var.db_name}-${var.environment}"), "_", "-")
  engine                 = "postgres"
  engine_version         = var.engine_version
  instance_class         = var.instance_class
  allocated_storage      = var.allocated_storage
  storage_type           = "gp3"
  db_name                = var.db_name
  username               = var.username
  password               = var.password
  db_subnet_group_name   = aws_db_subnet_group.this.name
  vpc_security_group_ids = var.vpc_security_group_ids
  publicly_accessible    = false
  skip_final_snapshot    = var.skip_final_snapshot
  storage_encrypted      = true

  tags = merge(
    {
      Component   = "Database-RDS"
      Environment = var.environment
      Name        = replace(lower("${var.db_name}-${var.environment}"), "_", "-")
    },
    var.tags
  )
}
