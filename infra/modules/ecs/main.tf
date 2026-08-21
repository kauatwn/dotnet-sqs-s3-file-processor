resource "aws_ecs_cluster" "this" {
  name = var.cluster_name

  setting {
    name  = "containerInsights"
    value = "enabled"
  }

  tags = merge(
    {
      Component   = "ECS-Cluster"
      Environment = var.environment
    },
    var.tags
  )
}

# CloudWatch Log Group for API
resource "aws_cloudwatch_log_group" "api" {
  name              = "/ecs/${var.cluster_name}/${var.api_task_config.name}"
  retention_in_days = var.log_retention_in_days

  tags = merge(
    {
      Component   = "ECS-Logging-API"
      Environment = var.environment
    },
    var.tags
  )
}

# CloudWatch Log Group for Worker
resource "aws_cloudwatch_log_group" "worker" {
  name              = "/ecs/${var.cluster_name}/${var.worker_task_config.name}"
  retention_in_days = var.log_retention_in_days

  tags = merge(
    {
      Component   = "ECS-Logging-Worker"
      Environment = var.environment
    },
    var.tags
  )
}

# IAM Execution Role for ECS Tasks (Pulling images, pushing logs, retrieving secrets)
resource "aws_iam_role" "execution_role" {
  name = "${var.cluster_name}-ecs-execution-role"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = "sts:AssumeRole"
        Effect = "Allow"
        Principal = {
          Service = "ecs-tasks.amazonaws.com"
        }
      }
    ]
  })

  tags = merge(
    {
      Component   = "IAM-ExecutionRole"
      Environment = var.environment
    },
    var.tags
  )
}

resource "aws_iam_role_policy_attachment" "execution_role_policy" {
  role       = aws_iam_role.execution_role.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy"
}

# Secrets Manager access for ECS Task Execution Role (when secrets are injected)
resource "aws_iam_role_policy" "execution_secrets_policy" {
  count = length(var.secret_arns) > 0 ? 1 : 0
  name  = "${var.cluster_name}-execution-secrets-policy"
  role  = aws_iam_role.execution_role.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "secretsmanager:GetSecretValue"
        ]
        Resource = var.secret_arns
      }
    ]
  })
}

# IAM Task Role for Application runtime (SQS, S3 access)
resource "aws_iam_role" "task_role" {
  name = "${var.cluster_name}-ecs-task-role"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = "sts:AssumeRole"
        Effect = "Allow"
        Principal = {
          Service = "ecs-tasks.amazonaws.com"
        }
      }
    ]
  })

  tags = merge(
    {
      Component   = "IAM-TaskRole"
      Environment = var.environment
    },
    var.tags
  )
}

# Policy allowing scoped SQS access for Task Role (Least Privilege)
resource "aws_iam_role_policy" "task_sqs_policy" {
  count = length(var.sqs_queue_arns) > 0 ? 1 : 0
  name  = "${var.cluster_name}-task-sqs-policy"
  role  = aws_iam_role.task_role.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "sqs:SendMessage",
          "sqs:ReceiveMessage",
          "sqs:DeleteMessage",
          "sqs:GetQueueAttributes",
          "sqs:GetQueueUrl"
        ]
        Resource = var.sqs_queue_arns
      }
    ]
  })
}

# Policy allowing S3 access for Task Role (Storage of CSV files)
resource "aws_iam_role_policy" "task_s3_policy" {
  count = var.s3_bucket_arn != null ? 1 : 0
  name  = "${var.cluster_name}-task-s3-policy"
  role  = aws_iam_role.task_role.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "s3:GetObject",
          "s3:PutObject",
          "s3:DeleteObject"
        ]
        Resource = "${var.s3_bucket_arn}/*"
      },
      {
        Effect = "Allow"
        Action = [
          "s3:ListBucket"
        ]
        Resource = var.s3_bucket_arn
      }
    ]
  })
}

# Task Definition - Web API (Fargate)
resource "aws_ecs_task_definition" "api" {
  family                   = "${var.cluster_name}-${var.api_task_config.name}"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = tostring(var.api_task_config.cpu)
  memory                   = tostring(var.api_task_config.memory)
  execution_role_arn       = aws_iam_role.execution_role.arn
  task_role_arn            = aws_iam_role.task_role.arn

  container_definitions = jsonencode([
    {
      name      = var.api_task_config.name
      image     = var.api_task_config.image
      cpu       = var.api_task_config.cpu
      memory    = var.api_task_config.memory
      essential = true
      portMappings = [
        {
          containerPort = var.api_task_config.container_port
          hostPort      = var.api_task_config.container_port
          protocol      = "tcp"
        }
      ]
      environment = [
        for key, value in var.api_task_config.environment_vars : {
          name  = key
          value = value
        }
      ]
      secrets = [
        for key, value in var.api_task_config.secrets : {
          name      = key
          valueFrom = value
        }
      ]
      logConfiguration = {
        logDriver = "awslogs"
        options = {
          "awslogs-group"         = aws_cloudwatch_log_group.api.name
          "awslogs-region"        = data.aws_region.current.name
          "awslogs-stream-prefix" = "api"
        }
      }
    }
  ])

  tags = merge(
    {
      Component   = "ECS-TaskDef-API"
      Environment = var.environment
    },
    var.tags
  )
}

# Task Definition - Background Worker (Fargate)
resource "aws_ecs_task_definition" "worker" {
  family                   = "${var.cluster_name}-${var.worker_task_config.name}"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = tostring(var.worker_task_config.cpu)
  memory                   = tostring(var.worker_task_config.memory)
  execution_role_arn       = aws_iam_role.execution_role.arn
  task_role_arn            = aws_iam_role.task_role.arn

  container_definitions = jsonencode([
    {
      name      = var.worker_task_config.name
      image     = var.worker_task_config.image
      cpu       = var.worker_task_config.cpu
      memory    = var.worker_task_config.memory
      essential = true
      environment = [
        for key, value in var.worker_task_config.environment_vars : {
          name  = key
          value = value
        }
      ]
      secrets = [
        for key, value in var.worker_task_config.secrets : {
          name      = key
          valueFrom = value
        }
      ]
      logConfiguration = {
        logDriver = "awslogs"
        options = {
          "awslogs-group"         = aws_cloudwatch_log_group.worker.name
          "awslogs-region"        = data.aws_region.current.name
          "awslogs-stream-prefix" = "worker"
        }
      }
    }
  ])

  tags = merge(
    {
      Component   = "ECS-TaskDef-Worker"
      Environment = var.environment
    },
    var.tags
  )
}

# ECS Service - Web API (Fargate)
resource "aws_ecs_service" "api" {
  name            = var.api_task_config.name
  cluster         = aws_ecs_cluster.this.id
  task_definition = aws_ecs_task_definition.api.arn
  desired_count   = var.api_task_config.desired_count
  launch_type     = "FARGATE"

  deployment_circuit_breaker {
    enable   = true
    rollback = true
  }

  deployment_minimum_healthy_percent = 100
  deployment_maximum_percent         = 200

  network_configuration {
    subnets          = var.subnet_ids
    security_groups  = var.security_group_ids
    assign_public_ip = var.assign_public_ip
  }

  tags = merge(
    {
      Component   = "ECS-Service-API"
      Environment = var.environment
    },
    var.tags
  )
}

# ECS Service - Background Worker (Fargate)
resource "aws_ecs_service" "worker" {
  name            = var.worker_task_config.name
  cluster         = aws_ecs_cluster.this.id
  task_definition = aws_ecs_task_definition.worker.arn
  desired_count   = var.worker_task_config.desired_count
  launch_type     = "FARGATE"

  deployment_circuit_breaker {
    enable   = true
    rollback = true
  }

  deployment_minimum_healthy_percent = 100
  deployment_maximum_percent         = 200

  network_configuration {
    subnets          = var.subnet_ids
    security_groups  = var.security_group_ids
    assign_public_ip = var.assign_public_ip
  }

  tags = merge(
    {
      Component   = "ECS-Service-Worker"
      Environment = var.environment
    },
    var.tags
  )
}

data "aws_region" "current" {}
