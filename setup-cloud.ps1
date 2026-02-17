# Agent 200: Cloud Setup Script
# This script helps judges and users safely generate an 'azure-deployment.yaml' from the template.

$templatePath = "azure-deployment.yaml.template"
$outputPath = "azure-deployment.yaml"

if (-not (Test-Path $templatePath)) {
    Write-Error "Template file '$templatePath' not found."
    exit
}

Write-Host "`n--- Agent 200: Hackathon Cloud Setup ---" -ForegroundColor Cyan
Write-Host "This script will generate a local (gitignored) deployment manifest.`n"

# 1. Collect Values
$subId = Read-Host "1. Enter Azure Subscription ID"
$tenantId = Read-Host "2. Enter Azure Tenant ID"
$envId = Read-Host "3. Enter Azure Container Apps Environment ID (Resource ID)"
$acrServer = Read-Host "4. Enter ACR Login Server (e.g., myacr.azurecr.io)"
$acrUser = Read-Host "5. Enter ACR Username"
$acrPass = Read-Host "6. Enter ACR Password"
$openaiEndpoint = Read-Host "7. Enter Azure OpenAI Endpoint (e.g., https://res.openai.azure.com/)"
$openaiKey = Read-Host "8. Enter Azure OpenAI Key"
$openaiDeployment = Read-Host "9. Enter Azure OpenAI Deployment Name (default: gpt-4o-mini)"
if ([string]::IsNullOrWhiteSpace($openaiDeployment)) { $openaiDeployment = "gpt-4o-mini" }
$githubToken = Read-Host "10. Enter GitHub Personal Access Token (PAT)"
$aiConnString = Read-Host "11. Enter App Insights Connection String (Optional, press Enter to skip)"

# 2. Read Template
$content = Get-Content $templatePath -Raw

# 3. Replace Placeholders
$content = $content.Replace("{{AZURE_SUBSCRIPTION_ID}}", $subId)
$content = $content.Replace("{{AZURE_TENANT_ID}}", $tenantId)
$content = $content.Replace("{{AZURE_ENVIRONMENT_ID}}", $envId)
$content = $content.Replace("{{ACR_SERVER}}", $acrServer)
$content = $content.Replace("{{ACR_USERNAME}}", $acrUser)
$content = $content.Replace("{{ACR_PASSWORD}}", $acrPass)
$content = $content.Replace("{{AZURE_OPENAI_ENDPOINT}}", $openaiEndpoint)
$content = $content.Replace("{{AZURE_OPENAI_KEY}}", $openaiKey)
$content = $content.Replace("{{AZURE_OPENAI_DEPLOYMENT}}", $openaiDeployment)
$content = $content.Replace("{{GITHUB_TOKEN}}", $githubToken)
$content = $content.Replace("{{AI_CONNECTION_STRING}}", $aiConnString)

# 4. Write Output
$content | Set-Content $outputPath

Write-Host "`n[SUCCESS] '$outputPath' generated successfully!" -ForegroundColor Green
Write-Host "You can now deploy using:" -ForegroundColor Yellow
Write-Host "az containerapp create --name agent200-host --resource-group YOUR_RG --yaml $outputPath`n"
