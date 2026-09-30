resource "aws_ecs_task_definition" "migration" {
  family                   = "${var.project_name}-migration"
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"

  cpu    = "256"
  memory = "512"

  execution_role_arn = data.aws_iam_role.ecs_task_execution.arn
  task_role_arn      = data.aws_iam_role.ecs_task_execution.arn

  container_definitions = jsonencode([
    {
      name      = "migration"
      image     = "${aws_ecr_repository.connecto_api.repository_url}:latest"
      essential = true

      environment = [
        {
          name  = "ASPNETCORE_ENVIRONMENT"
          value = var.environment
        }
      ]

      secrets = [
        {
          name      = "ConnectionStrings__DefaultConnection"
          valueFrom = aws_db_instance.connecto.master_user_secret[0].secret_arn
        }
      ]

      logConfiguration = {
        logDriver = "awslogs"

        options = {
          awslogs-group         = aws_cloudwatch_log_group.connecto.name
          awslogs-region        = var.aws_region
          awslogs-stream-prefix = "migration"
        }
      }
    }
  ])

  tags = {
    Name        = "${var.project_name}-migration"
    Environment = var.environment
  }
}