.PHONY: help up down status tf-init ecr-init docker-build docker-push tf-apply tf-destroy deploy clean

.DEFAULT_GOAL := help

# ==============================================================================
# Configuration & Variables
# ==============================================================================
SHELL := /bin/bash
IMAGE_TAG ?= latest
LOCALSTACK_ENDPOINT ?= http://localhost:4566
DEV_INFRA_DIR := infra/environments/dev

API_IMAGE := fileprocessor-api-dev
WORKER_IMAGE := fileprocessor-worker-dev
LOCALSTACK_ECR := 000000000000.dkr.ecr.us-east-1.localhost.localstack.cloud:4566

# ==============================================================================
# Help
# ==============================================================================
help: ## Show this help menu
	@echo "======================================================================"
	@echo " 🚀 Cloud-Native Bulk Ingestion - Local Dev Environment"
	@echo "======================================================================"
	@grep -E '^[a-zA-Z_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | awk 'BEGIN {FS = ":.*?## "}; {printf "  \033[36m%-15s\033[0m %s\n", $$1, $$2}'

# ==============================================================================
# Containers Base (LocalStack & DB)
# ==============================================================================
up: ## Start PostgreSQL and LocalStack
	@echo "🐳 Starting PostgreSQL and LocalStack..."
	@docker compose up -d postgres localstack
	@echo "⏳ Waiting for LocalStack to be healthy..."
	@docker compose exec -T localstack curl -s -f http://localhost:4566/_localstack/health > /dev/null || sleep 5
	@echo "✅ Local infrastructure is ready!"

down: ## Stop local containers
	@echo "🛑 Stopping containers..."
	@docker compose down

status: ## Check running containers
	@docker compose ps

# ==============================================================================
# Infrastructure & Deploy Flow
# ==============================================================================
tf-init: ## Initialize Terraform
	@cd $(DEV_INFRA_DIR) && terraform init

ecr-init: ## Provision ECR repositories first
	@cd $(DEV_INFRA_DIR) && terraform apply -target="module.ecr_api.aws_ecr_repository.this" -target="module.ecr_worker.aws_ecr_repository.this" -var="localstack_endpoint=$(LOCALSTACK_ENDPOINT)" -auto-approve

docker-build: ## Build API and Worker images
	@docker build -q -t $(LOCALSTACK_ECR)/$(API_IMAGE):$(IMAGE_TAG) -t $(API_IMAGE):$(IMAGE_TAG) -f src/DistributedFileProcessor.API/Dockerfile .
	@docker build -q -t $(LOCALSTACK_ECR)/$(WORKER_IMAGE):$(IMAGE_TAG) -t $(WORKER_IMAGE):$(IMAGE_TAG) -f src/DistributedFileProcessor.Worker/Dockerfile .

docker-push: ## Push images to LocalStack ECR
	@docker push -q $(LOCALSTACK_ECR)/$(API_IMAGE):$(IMAGE_TAG)
	@docker push -q $(LOCALSTACK_ECR)/$(WORKER_IMAGE):$(IMAGE_TAG)

tf-apply: ## Apply full infrastructure (S3, SQS, ECS, RDS)
	@cd $(DEV_INFRA_DIR) && terraform apply -var="localstack_endpoint=$(LOCALSTACK_ENDPOINT)" -var="image_tag=$(IMAGE_TAG)" -auto-approve

deploy: up tf-init ecr-init docker-build docker-push tf-apply ## Full automated setup: start localstack, build, push, and apply infra
	@echo "🎉 Environment successfully deployed on LocalStack!"

# ==============================================================================
# Teardown
# ==============================================================================
tf-destroy: ## Destroy Terraform resources
	@cd $(DEV_INFRA_DIR) && terraform destroy -var="localstack_endpoint=$(LOCALSTACK_ENDPOINT)" -var="image_tag=$(IMAGE_TAG)" -auto-approve

clean: tf-destroy down ## Destroy infra, stop containers and clear artifacts
	@rm -rf tools/output bin obj infra/environments/dev/.terraform infra/environments/dev/terraform.tfstate*
	@echo "🧹 Environment cleaned!"
