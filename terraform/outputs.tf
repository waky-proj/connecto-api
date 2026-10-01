output "migration_task_definition" {
  value = aws_ecs_task_definition.migration.family
}
output "migration_subnet_ids" {
  value = join(",", var.migration_subnet_ids)
}
output "migration_security_group" {
  value = aws_security_group.migration.id
}
output "database_server" {
  value = "${aws_db_instance.connecto.address},${aws_db_instance.connecto.port}"
}
output "database_name" {
  value = var.database_name
}
output "database_credentials_secret_arn" {
  value = aws_db_instance.connecto.master_user_secret[0].secret_arn
}
