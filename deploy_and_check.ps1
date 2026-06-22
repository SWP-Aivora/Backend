# Script để deploy và check CI/CD status trên GitHub
# Sử dụng: .\deploy_and_check.ps1

Write-Host "🚀 Starting deployment process..." -ForegroundColor Green

# Step 1: Add và commit changes
Write-Host "📝 Step 1: Staging and committing changes..." -ForegroundColor Yellow
git add .
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Failed to add changes" -ForegroundColor Red
    exit 1
}

# Get commit message từ user hoặc dùng default
$commit_msg = Read-Host "Enter commit message (default: 'Auto deploy')"
if ([string]::IsNullOrWhiteSpace($commit_msg)) {
    $commit_msg = "Auto deploy"
}

git commit -m $commit_msg
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Failed to commit changes" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Committed with message: $commit_msg" -ForegroundColor Green

# Step 2: Push to remote
Write-Host "📤 Step 2: Pushing to remote..." -ForegroundColor Yellow
git push
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Failed to push to remote" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Pushed successfully" -ForegroundColor Green

# Step 3: Check CI/CD status
Write-Host "🔍 Step 3: Checking CI/CD status..." -ForegroundColor Yellow
Write-Host "Waiting for CI to start..." -ForegroundColor Gray

# Wait 10 seconds để CI bắt đầu
Start-Sleep -Seconds 10

# Check status
$workflow = gh run list --limit 1 --json databaseId --jq '.[0]'

if ($workflow -eq "null") {
    Write-Host "❌ Could not get workflow ID" -ForegroundColor Red
    exit 1
}

$workflow_id = $workflow.databaseId
Write-Host "📊 Monitoring workflow ID: $workflow_id" -ForegroundColor Cyan

# Monitor status
while ($true) {
    $status = gh run view $workflow_id --json status --jq '.status'

    switch ($status) {
        "success" {
            Write-Host "✅ CI/CD completed successfully!" -ForegroundColor Green
            break
        }
        "failure" {
            Write-Host "❌ CI/CD failed with status: $status" -ForegroundColor Red
            $conclusion = gh run view $workflow_id --json conclusion --jq '.conclusion'
            Write-Host "Conclusion: $conclusion" -ForegroundColor Red
            exit 1
        }
        "cancelled" {
            Write-Host "❌ CI/CD was cancelled" -ForegroundColor Red
            exit 1
        }
        "timed_out" {
            Write-Host "❌ CI/CD timed out" -ForegroundColor Red
            exit 1
        }
        "action_required" {
            Write-Host "❌ CI/CD requires manual action" -ForegroundColor Red
            exit 1
        }
        default {
            Write-Host "⏳ CI/CD is running... Status: $status" -ForegroundColor Yellow
            Start-Sleep -Seconds 10
        }
    }
}

# Step 4: Show workflow summary
Write-Host "" -ForegroundColor White
Write-Host "📋 Workflow Summary:" -ForegroundColor White
Write-Host "-------------------" -ForegroundColor White
$details = gh run view $workflow_id --json databaseId,status,conclusion,createdAt,updatedAt --jq '.databaseId, .status, .conclusion, .createdAt, .updatedAt'
Write-Host $details -ForegroundColor White

Write-Host "" -ForegroundColor White
Write-Host "🎉 Deployment process completed!" -ForegroundColor Green