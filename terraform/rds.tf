resource "aws_db_subnet_group" "connecto" {
  name = "${var.project_name}-db-subnet-group"

  subnet_ids = var.ecs_subnet_ids

  tags = {
    Name        = "${var.project_name}-db-subnet-group"
    Environment = var.environment
  }
}

resource "aws_db_instance" "connecto" {
  identifier = "${var.project_name}-db"

  engine         = "sqlserver-ex"
  engine_version = "15.00"

  instance_class        = "db.t3.micro"
  allocated_storage     = 20
  max_allocated_storage = 50
  storage_type          = "gp3"

  db_name  = "Connecto"
  username = var.rds_username

  manage_master_user_password = true

  db_subnet_group_name = aws_db_subnet_group.connecto.name
  vpc_security_group_ids = [
    aws_security_group.rds.id
  ]

  publicly_accessible = false

  backup_retention_period = 1

  skip_final_snapshot = true

  deletion_protection = false

  tags = {
    Name        = "${var.project_name}-db"
    Environment = var.environment
  }
}