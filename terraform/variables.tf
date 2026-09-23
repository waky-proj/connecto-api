variable "aws_region" {
  description = "AWS region where the infrastructure will be deployed."
  type        = string
  default     = "ap-southeast-2"
}

variable "project_name" {
  description = "Project name used for resource naming."
  type        = string
  default     = "connecto"
}

variable "environment" {
  description = "Deployment environment."
  type        = string
  default     = "production"

  validation {
    condition     = contains(["development", "staging", "production"], var.environment)
    error_message = "Environment must be development, staging, or production."
  }
}

variable "ecr_repository_name" {
  description = "Name of the ECR repository."
  type        = string
  default     = "connecto-api"
}

variable "ecs_cluster_name" {
  description = "Name of the ECS cluster."
  type        = string
  default     = "connecto"
}

variable "ecs_service_name" {
  description = "Name of the ECS service."
  type        = string
  default     = "connecto-api"
}

variable "ecs_subnet_ids" {
  type = list(string)
}

variable "container_name" {
  description = "Name of the application container."
  type        = string
  default     = "connecto-api"
}

variable "alb_name" {
  description = "Application Load Balancer name."
  type        = string
  default     = "connecto-api-alb"
}

variable "alb_listener_port" {
  type    = number
  default = 80
}

variable "alb_subnet_ids" {
  type = list(string)
}

variable "container_port" {
  description = "Port exposed by the ASP.NET Core container."
  type        = number
  default     = 8080
}

variable "target_group_name" {
  description = "ALB target group name."
  type        = string
  default     = "connecto-api-tg"
}

variable "health_check_path" {
  description = "HTTP path used by the ALB health check."
  type        = string
  default     = "/health"
}

variable "desired_count" {
  description = "Number of ECS tasks to run."
  type        = number
  default     = 1
}

variable "cpu" {
  description = "ECS task CPU units."
  type        = number
  default     = 512
}

variable "memory" {
  description = "ECS task memory in MiB."
  type        = number
  default     = 1024
}

variable "vpc_id" {
  description = "VPC ID where the ECS service and ALB will run."
  type        = string
}

variable "public_subnet_ids" {
  description = "Public subnet IDs used by the Application Load Balancer."
  type        = list(string)
}

variable "rds_username" {
  type    = string
  default = "connectoadmin"
}