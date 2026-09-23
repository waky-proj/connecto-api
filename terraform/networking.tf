data "aws_vpc" "connecto" {
  id = var.vpc_id
}

data "aws_subnet" "public" {
  for_each = toset(var.public_subnet_ids)

  id = each.value
}
