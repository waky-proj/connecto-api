variable "media_upload_origins" {
  description = "Exact HTTPS browser origins permitted to upload to private quarantine"
  type        = list(string)
  default     = []
}

resource "aws_s3_bucket" "media" {
  bucket_prefix = "${var.project_name}-${var.environment}-media-"
  force_destroy = false
  tags          = { Environment = var.environment, Purpose = "private-media" }
}

resource "aws_s3_bucket_public_access_block" "media" {
  bucket                  = aws_s3_bucket.media.id
  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_server_side_encryption_configuration" "media" {
  bucket = aws_s3_bucket.media.id
  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
  }
}

resource "aws_s3_bucket_cors_configuration" "media" {
  count  = length(var.media_upload_origins) > 0 ? 1 : 0
  bucket = aws_s3_bucket.media.id
  cors_rule {
    allowed_headers = ["*"]
    allowed_methods = ["POST"]
    allowed_origins = var.media_upload_origins
    max_age_seconds = 300
  }
}

resource "aws_s3_bucket_lifecycle_configuration" "media" {
  bucket = aws_s3_bucket.media.id
  rule {
    id     = "abandoned-quarantine"
    status = "Enabled"
    filter { prefix = "quarantine/" }
    expiration { days = 1 }
  }
}

resource "aws_iam_role" "media_task" {
  name = "${var.project_name}-${var.environment}-media-task"
  assume_role_policy = jsonencode({
    Version   = "2012-10-17"
    Statement = [{ Effect = "Allow", Principal = { Service = "ecs-tasks.amazonaws.com" }, Action = "sts:AssumeRole" }]
  })
}

resource "aws_iam_role_policy" "media_objects" {
  name = "private-media-objects"
  role = aws_iam_role.media_task.id
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect   = "Allow"
      Action   = ["s3:PutObject", "s3:GetObject", "s3:DeleteObject"]
      Resource = ["${aws_s3_bucket.media.arn}/quarantine/*", "${aws_s3_bucket.media.arn}/display/*"]
    }]
  })
}

output "media_bucket_name" { value = aws_s3_bucket.media.bucket }
output "media_task_role_arn" { value = aws_iam_role.media_task.arn }
