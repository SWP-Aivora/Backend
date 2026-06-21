#!/bin/bash

# Script để deploy và check CI/CD status trên GitHub
# Sử dụng: ./deploy_and_check.sh

echo "🚀 Starting deployment process..."

# Step 1: Add và commit changes
echo "📝 Step 1: Staging and committing changes..."
git add .
if [ $? -ne 0 ]; then
    echo "❌ Failed to add changes"
    exit 1
fi

# Get commit message từ user hoặc dùng default
read -p "Enter commit message (default: 'Auto deploy'): " commit_msg
commit_msg=${commit_msg:-"Auto deploy"}

git commit -m "$commit_msg"
if [ $? -ne 0 ]; then
    echo "❌ Failed to commit changes"
    exit 1
fi

echo "✅ Committed with message: $commit_msg"

# Step 2: Push to remote
echo "📤 Step 2: Pushing to remote..."
git push
if [ $? -ne 0 ]; then
    echo "❌ Failed to push to remote"
    exit 1
fi

echo "✅ Pushed successfully"

# Step 3: Check CI/CD status
echo "🔍 Step 3: Checking CI/CD status..."
echo "Waiting for CI to start..."

# Wait 10 seconds để CI bắt đầu
sleep 10

# Check status
workflow_id=$(gh run list --limit 1 --json databaseId --jq '.[0].databaseId')

if [ -z "$workflow_id" ]; then
    echo "❌ Could not get workflow ID"
    exit 1
fi

echo "📊 Monitoring workflow ID: $workflow_id"

# Monitor status
while true; do
    status=$(gh run view $workflow_id --json status --jq '.status')

    case $status in
        "success")
            echo "✅ CI/CD completed successfully!"
            break
            ;;
        "failure"|"cancelled"|"timed_out"|"action_required")
            echo "❌ CI/CD failed with status: $status"
            gh run view $workflow_id --json conclusion --jq '.conclusion'
            exit 1
            ;;
        *)
            echo "⏳ CI/CD is running... Status: $status"
            sleep 10
            ;;
    esac
done

# Step 4: Show workflow summary
echo ""
echo "📋 Workflow Summary:"
echo "-------------------"
gh run view $workflow_id --json databaseId,status,conclusion,createdAt,updatedAt --jq '.databaseId, .status, .conclusion, .createdAt, .updatedAt'

echo ""
echo "🎉 Deployment process completed!"