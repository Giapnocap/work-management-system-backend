[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()]
    [string] $BaseUrl = "http://localhost:8080",

    [ValidateNotNullOrEmpty()]
    [string] $Password = "Demo@123456"
)

$ErrorActionPreference = "Stop"
$BaseUrl = $BaseUrl.TrimEnd("/")

function Invoke-JsonApi {
    param(
        [Parameter(Mandatory)]
        [ValidateSet("Get", "Post")]
        [string] $Method,

        [Parameter(Mandatory)]
        [string] $Uri,

        [string] $Token,

        [hashtable] $Body
    )

    $request = @{
        Method = $Method
        Uri = $Uri
    }

    if ($Token) {
        $request.Headers = @{ Authorization = "Bearer $Token" }
    }

    if ($null -ne $Body) {
        $request.ContentType = "application/json"
        $request.Body = $Body | ConvertTo-Json -Depth 6
    }

    Invoke-RestMethod @request
}

function Get-AccessToken {
    param(
        [Parameter(Mandatory)]
        [string] $Username
    )

    $response = Invoke-JsonApi `
        -Method Post `
        -Uri "$BaseUrl/api/auth/login" `
        -Body @{ username = $Username; password = $Password }

    if ($response -is [string]) {
        return $response.Trim('"')
    }

    foreach ($propertyName in @("accessToken", "token")) {
        $property = $response.PSObject.Properties[$propertyName]
        if ($null -ne $property -and -not [string]::IsNullOrWhiteSpace([string] $property.Value)) {
            return [string] $property.Value
        }
    }

    throw "Login response for '$Username' did not contain an access token."
}

function Send-EvidenceFile {
    param(
        [Parameter(Mandatory)]
        [string] $Token,

        [Parameter(Mandatory)]
        [Guid] $TaskId,

        [Parameter(Mandatory)]
        [string] $Path
    )

    $curl = Get-Command curl.exe -ErrorAction SilentlyContinue
    if ($null -eq $curl) {
        throw "curl.exe is required for the multipart upload step."
    }

    $responseBody = & $curl.Source `
        --silent `
        --show-error `
        --fail-with-body `
        --request POST `
        --url "$BaseUrl/api/Upload?taskId=$TaskId" `
        --header "Authorization: Bearer $Token" `
        --form "file=@$Path;type=text/plain"

    if ($LASTEXITCODE -ne 0) {
        throw "Evidence upload failed with curl exit code $LASTEXITCODE."
    }

    $responseBody | ConvertFrom-Json
}

function Assert-Equal {
    param(
        [Parameter(Mandatory)]
        [object] $Actual,

        [Parameter(Mandatory)]
        [object] $Expected,

        [Parameter(Mandatory)]
        [string] $Message
    )

    if ($Actual -ne $Expected) {
        throw "$Message Expected '$Expected', received '$Actual'."
    }
}

function Assert-NotEmpty {
    param(
        [object] $Value,

        [Parameter(Mandatory)]
        [string] $Message
    )

    if ($null -eq $Value -or [string]::IsNullOrWhiteSpace([string] $Value)) {
        throw $Message
    }
}

$evidencePath = Join-Path ([System.IO.Path]::GetTempPath()) "wms-demo-evidence-$([Guid]::NewGuid().ToString('N')).txt"

try {
    Write-Host "[1/9] Checking API readiness"
    Invoke-RestMethod -Method Get -Uri "$BaseUrl/health/ready" | Out-Null

    Write-Host "[2/9] Signing in as the seeded Manager and User"
    $managerToken = Get-AccessToken -Username "demo.manager"
    $employeeToken = Get-AccessToken -Username "demo.employee1"
    Assert-NotEmpty -Value $managerToken -Message "Manager access token is empty."
    Assert-NotEmpty -Value $employeeToken -Message "Employee access token is empty."

    Write-Host "[3/9] Resolving the employee inside the Manager's department"
    $employees = Invoke-JsonApi `
        -Method Get `
        -Uri "$BaseUrl/api/users/search?keyword=demo.employee1&role=User" `
        -Token $managerToken
    $employee = @($employees) | Select-Object -First 1
    if ($null -eq $employee) {
        throw "The demo employee is not visible to the demo Manager."
    }

    $runId = Get-Date -Format "yyyyMMdd-HHmmss-fff"

    Write-Host "[4/9] Creating a department project"
    $project = Invoke-JsonApi `
        -Method Post `
        -Uri "$BaseUrl/api/projects" `
        -Token $managerToken `
        -Body @{
            name = "Backend API walkthrough $runId"
            description = "Project created by the repeatable portfolio demo"
        }
    Assert-NotEmpty -Value $project.id -Message "Project creation did not return an id."

    Write-Host "[5/9] Creating and assigning a review-required task"
    $task = Invoke-JsonApi `
        -Method Post `
        -Uri "$BaseUrl/api/tasks" `
        -Token $managerToken `
        -Body @{
            title = "Verify backend workflow $runId"
            description = "Upload evidence, submit completion, and request Manager review"
            dueDate = (Get-Date).ToUniversalTime().AddDays(2).ToString("o")
            userIds = @($employee.id)
            unitIds = @()
            priority = "High"
            requiresReview = $true
            plannedEffortHours = 2
            projectId = $project.id
        }
    Assert-Equal -Actual $task.status -Expected "NotStarted" -Message "A newly created task has the wrong status."

    Write-Host "[6/9] Uploading task-scoped evidence"
    Set-Content -LiteralPath $evidencePath -Value "Portfolio demo evidence for $runId" -Encoding utf8
    $upload = Send-EvidenceFile -Token $employeeToken -TaskId $task.id -Path $evidencePath
    Assert-Equal -Actual $upload.taskId -Expected $task.id -Message "The evidence file is linked to the wrong task."

    Write-Host "[7/9] Submitting a 100 percent progress report"
    $progress = Invoke-JsonApi `
        -Method Post `
        -Uri "$BaseUrl/api/progress" `
        -Token $employeeToken `
        -Body @{
            taskId = $task.id
            percent = 100
            description = "Completed and ready for review"
            hoursSpent = 2
            fileId = $upload.id
        }
    Assert-Equal -Actual $progress.status -Expected "Submitted" -Message "The review-required report was not submitted."

    Write-Host "[8/9] Approving the report as the department Manager"
    $review = Invoke-JsonApi `
        -Method Post `
        -Uri "$BaseUrl/api/review" `
        -Token $managerToken `
        -Body @{
            progressId = $progress.id
            approve = $true
            comment = "Evidence accepted by the portfolio demo"
        }
    Assert-Equal -Actual $review.progressId -Expected $progress.id -Message "The review response references the wrong progress report."

    Write-Host "[9/9] Verifying approved task, timeline, and KPI read model"
    $approvedTask = Invoke-JsonApi -Method Get -Uri "$BaseUrl/api/tasks/$($task.id)" -Token $employeeToken
    $taskProgress = Invoke-JsonApi -Method Get -Uri "$BaseUrl/api/progress/task/$($task.id)" -Token $employeeToken
    $timeline = Invoke-JsonApi -Method Get -Uri "$BaseUrl/api/tasks/$($task.id)/timeline?size=20" -Token $employeeToken
    $performance = Invoke-JsonApi -Method Get -Uri "$BaseUrl/api/users/performance/$($employee.id)" -Token $employeeToken

    Assert-Equal -Actual $approvedTask.status -Expected "Approved" -Message "The approved report did not complete the task."
    $approvedProgress = @($taskProgress) | Where-Object { $_.id -eq $progress.id } | Select-Object -First 1
    if ($null -eq $approvedProgress) {
        throw "The created progress report was not returned by the task progress endpoint."
    }
    Assert-Equal -Actual $approvedProgress.status -Expected "Approved" -Message "The persisted progress report was not approved."
    if (@($timeline.items).Count -lt 4) {
        throw "The task timeline did not expose the expected workflow events."
    }
    Assert-Equal -Actual $performance.userId -Expected $employee.id -Message "The KPI response belongs to the wrong user."

    Write-Host ""
    Write-Host "Demo workflow passed." -ForegroundColor Green
    [PSCustomObject]@{
        ProjectId = $project.id
        TaskId = $approvedTask.id
        TaskStatus = $approvedTask.status
        ProgressId = $progress.id
        ProgressStatus = $approvedProgress.status
        TimelineEvents = @($timeline.items).Count
        KpiScore = $performance.score
    } | Format-List
}
finally {
    if (Test-Path -LiteralPath $evidencePath) {
        Remove-Item -LiteralPath $evidencePath -Force
    }
}
