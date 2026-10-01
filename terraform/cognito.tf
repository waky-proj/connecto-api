resource "aws_cognito_user_pool" "connecto" {
  name                     = "${var.project_name}-${var.environment}"
  username_attributes      = ["email"]
  auto_verified_attributes = ["email"]

  username_configuration {
    case_sensitive = false
  }

  password_policy {
    minimum_length                   = 12
    require_lowercase                = true
    require_uppercase                = true
    require_numbers                  = true
    require_symbols                  = true
    temporary_password_validity_days = 7
  }

  account_recovery_setting {
    recovery_mechanism {
      name     = "verified_email"
      priority = 1
    }
  }

  tags = {
    Name        = "${var.project_name}-identity"
    Environment = var.environment
  }
}

resource "aws_cognito_user_pool_client" "connecto" {
  name                          = "${var.project_name}-web"
  user_pool_id                  = aws_cognito_user_pool.connecto.id
  generate_secret               = false
  prevent_user_existence_errors = "ENABLED"
  enable_token_revocation       = true
  explicit_auth_flows           = ["ALLOW_USER_PASSWORD_AUTH", "ALLOW_USER_SRP_AUTH", "ALLOW_REFRESH_TOKEN_AUTH"]
  access_token_validity         = 60
  id_token_validity             = 60
  refresh_token_validity        = 30

  token_validity_units {
    access_token  = "minutes"
    id_token      = "minutes"
    refresh_token = "days"
  }
}

locals {
  cognito_issuer = "https://${aws_cognito_user_pool.connecto.endpoint}"
}

output "cognito_user_pool_id" {
  value = aws_cognito_user_pool.connecto.id
}

output "cognito_issuer" {
  value = local.cognito_issuer
}

output "cognito_app_client_id" {
  value = aws_cognito_user_pool_client.connecto.id
}

output "cognito_ecs_environment" {
  description = "API task environment values; deployment uses GitHub environment variables with these values."
  value = {
    Cognito__Issuer   = local.cognito_issuer
    Cognito__Audience = aws_cognito_user_pool_client.connecto.id
  }
}
