[CmdletBinding()]
param(
    [string]$ComposeProjectName = "work-management",
    [string]$DatabaseName = "WorkManagementDB"
)

$ErrorActionPreference = "Stop"

$containerIds = @(& docker ps --filter "label=com.docker.compose.project=$ComposeProjectName" --filter "label=com.docker.compose.service=sqlserver" --format "{{.ID}}")
if ($LASTEXITCODE -ne 0) {
    throw "Could not query Docker for the SQL Server container."
}
$containerId = $containerIds | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($containerId)) {
    throw "The SQL Server service for Compose project '$ComposeProjectName' is not running."
}

$containerPassword = (& docker exec $containerId printenv MSSQL_SA_PASSWORD | Select-Object -First 1).Trim()
if ([string]::IsNullOrWhiteSpace($containerPassword)) {
    throw "The SQL Server container does not expose MSSQL_SA_PASSWORD."
}

$sqlcmdPath = "/opt/mssql-tools18/bin/sqlcmd"
& docker exec $containerId test -x $sqlcmdPath
if ($LASTEXITCODE -ne 0) {
    $sqlcmdPath = "/opt/mssql-tools/bin/sqlcmd"
    & docker exec $containerId test -x $sqlcmdPath
    if ($LASTEXITCODE -ne 0) {
        throw "sqlcmd was not found in the SQL Server container."
    }
}

$suffix = [Guid]::NewGuid().ToString("N")
$restoreDatabaseName = "WmsRestoreDrill_$suffix"
$backupPath = "/var/opt/mssql/backup/wms-recovery-$suffix.bak"

function Invoke-SqlCommand {
    param([Parameter(Mandatory)][string]$Query)

    $output = & docker exec -e "SQLCMDPASSWORD=$containerPassword" $containerId $sqlcmdPath -S localhost -U sa -C -b -r 1 -h -1 -W -s "|" -Q $Query
    if ($LASTEXITCODE -ne 0) {
        throw "SQL Server command failed."
    }

    return @($output)
}

function Escape-SqlIdentifier {
    param([Parameter(Mandatory)][string]$Value)
    return $Value.Replace("]", "]]")
}

function Escape-SqlLiteral {
    param([Parameter(Mandatory)][string]$Value)
    return $Value.Replace("'", "''")
}

$sourceDatabase = Escape-SqlIdentifier $DatabaseName
$restoreDatabase = Escape-SqlIdentifier $restoreDatabaseName
$backupLiteral = Escape-SqlLiteral $backupPath

try {
    & docker exec $containerId mkdir -p /var/opt/mssql/backup
    if ($LASTEXITCODE -ne 0) {
        throw "Could not create the SQL Server backup directory."
    }

    Write-Host "Backing up [$DatabaseName] with checksum..."
    Invoke-SqlCommand "BACKUP DATABASE [$sourceDatabase] TO DISK = N'$backupLiteral' WITH COPY_ONLY, INIT, CHECKSUM;"
    Invoke-SqlCommand "RESTORE VERIFYONLY FROM DISK = N'$backupLiteral' WITH CHECKSUM;"

    $fileList = Invoke-SqlCommand "RESTORE FILELISTONLY FROM DISK = N'$backupLiteral';"
    $fileRows = @($fileList | ForEach-Object { $_.Trim() } | Where-Object { $_ -match '\|[DL]\|' })
    if ($fileRows.Count -eq 0) {
        throw "RESTORE FILELISTONLY did not return any database files."
    }

    $moveClauses = [System.Collections.Generic.List[string]]::new()
    $dataIndex = 0
    $logIndex = 0
    foreach ($row in $fileRows) {
        $fields = $row -split '\|'
        $logicalName = Escape-SqlLiteral $fields[0].Trim()
        $fileType = $fields[2].Trim()
        if ($fileType -eq "L") {
            $logIndex++
            $targetPath = "/var/opt/mssql/data/${restoreDatabaseName}_log_$logIndex.ldf"
        }
        else {
            $dataIndex++
            $extension = if ($dataIndex -eq 1) { "mdf" } else { "ndf" }
            $targetPath = "/var/opt/mssql/data/${restoreDatabaseName}_data_$dataIndex.$extension"
        }

        $targetLiteral = Escape-SqlLiteral $targetPath
        $moveClauses.Add("MOVE N'$logicalName' TO N'$targetLiteral'")
    }

    Write-Host "Restoring into temporary database [$restoreDatabaseName]..."
    $moves = [string]::Join(", ", $moveClauses)
    Invoke-SqlCommand "RESTORE DATABASE [$restoreDatabase] FROM DISK = N'$backupLiteral' WITH $moves, RECOVERY, CHECKSUM;"

    $countQuery = @"
SET NOCOUNT ON;
SELECT CONCAT(
    (SELECT COUNT_BIG(*) FROM [{0}].[dbo].[__EFMigrationsHistory]), N'|',
    (SELECT COUNT_BIG(*) FROM [{0}].[dbo].[Units]), N'|',
    (SELECT COUNT_BIG(*) FROM [{0}].[dbo].[Users]), N'|',
    (SELECT COUNT_BIG(*) FROM [{0}].[dbo].[Tasks]), N'|',
    (SELECT COUNT_BIG(*) FROM [{0}].[dbo].[KpiResults]));
"@
    $sourceCounts = Invoke-SqlCommand ([string]::Format($countQuery, $sourceDatabase)) |
        Where-Object { $_.Trim() -match '^\d+\|\d+\|\d+\|\d+\|\d+$' } |
        Select-Object -First 1
    $restoredCounts = Invoke-SqlCommand ([string]::Format($countQuery, $restoreDatabase)) |
        Where-Object { $_.Trim() -match '^\d+\|\d+\|\d+\|\d+\|\d+$' } |
        Select-Object -First 1

    if ([string]::IsNullOrWhiteSpace($sourceCounts) -or $sourceCounts.Trim() -ne $restoredCounts.Trim()) {
        throw "Critical record counts differ after restore. Source='$sourceCounts'; restored='$restoredCounts'."
    }

    $counts = $restoredCounts.Trim() -split '\|'
    if ([long]$counts[0] -le 0 -or [long]$counts[2] -le 0) {
        throw "The drill requires an initialized database with migrations and at least one user."
    }

    Invoke-SqlCommand "DBCC CHECKDB (N'$($restoreDatabaseName.Replace("'", "''"))') WITH NO_INFOMSGS, ALL_ERRORMSGS;"
    Write-Host "Recovery drill passed. Counts (migrations|units|users|tasks|kpi): $($restoredCounts.Trim())"
}
finally {
    try {
        Invoke-SqlCommand "IF DB_ID(N'$($restoreDatabaseName.Replace("'", "''"))') IS NOT NULL BEGIN ALTER DATABASE [$restoreDatabase] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$restoreDatabase]; END"
    }
    catch {
        Write-Warning "Could not remove temporary database [$restoreDatabaseName]: $($_.Exception.Message)"
    }

    & docker exec $containerId rm -f $backupPath | Out-Null
}
