resource "aws_iam_role_policy" "ecs_secrets" {
  name = "${var.project_name}-ecs-secrets"
  role = aws_iam_role.ecs_task_execution.id

  policy = jsonencode({
    Version = "2012-10-17"

    Statement = [
      {
        Effect = "Allow"

        Action = [
          "secretsmanager:GetSecretValue"
        ]

        Resource = aws_db_instance.connecto.master_user_secret[0].secret_arn
      }
    ]
  })
}