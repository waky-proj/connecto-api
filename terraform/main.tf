resource "aws_ecr_repository" "connecto_api" {
  name = var.ecr_repository_name
  
  image_scanning_configuration {
	scan_on_push = true
  }
}
