output "workload_resource_group" {
  description = "Resource group details for the workload from platform-workloads state"
  value       = local.workload_resource_group
}

output "workload_backend" {
  description = "Terraform backend configuration for the workload from platform-workloads state"
  value       = local.workload_backend
}
