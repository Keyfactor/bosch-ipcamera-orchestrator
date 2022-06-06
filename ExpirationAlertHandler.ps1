# Check and process the context parameters
$certLocations = $context["Locations"]
$certDN = $context["DN"]
$outputLog = $true
$host = $context["Host"]

# Generate a log file for tracing
if ($outputLog) { $outputFile = ("C:\Keyfactor\logs\expiration-handler" + (get-date -UFormat "%Y%m%d%H%M") + ".txt") }
if ($outputLog) { Add-Content -Path $outputFile -Value "Starting Trace: $(Get-Date -format G)" }

# Locations come in in the format "ClientMachine - StorePath"
$locationSplit = $certLocations.Split(' ')
$clientMachine = $locationSplit[0]
$clientPath = $locationSplit[2]

# First API call to look up the certificate store that needs re-enrollment
$uri = "$($host)/KeyfactorAPI/CertificateStores?query=ClientMachine%20-eq%20%22$($clientMachine)%22%20AND%20StorePath%20-eq%20%22$($clientPath)%22"
if ($outputLog) { Add-Content -Path $outputFile -Value "Making REST call to $($uri)" }
$response = Invoke-RestMethod -Uri $uri -Method GET -UseDefaultCredentials -ContentType 'application/json'
if ($outputLog) { Add-Content -Path $outputFile -Value "Got back $($response)" }
if ($response.Count -ne 1) { throw "Expected to find 1 cert store, found $($response.Count) instead. URL: $($url)" }
$storeId = $response[0].Id
$agentId = $response[0].AgentId
if ($outputLog) { Add-Content -Path $outputFile -Value "StoreId $($storeId) AgentId $($agentID)" }
$subject =  $certDN
$alias = "test"

# Second API call to schedule the re-enrollment job on the orchestrator
$uri = "$($host)/KeyfactorAPI/CertificateStores/reenrollment"
$reenrollBody = @{ "KeystoreId" = $storeId ; "AgentGuid" = $agentId; "SubjectName" = $subject;"Alias" = $alias } | ConvertTo-Json -Compress
if ($outputLog) { Add-Content -Path $outputFile -Value "Posting to $($uri) with body $($reenrollBody)" }
$response = Invoke-RestMethod -Uri $uri -Method POST -Body $reenrollBody -UseDefaultCredentials -ContentType 'application/json' -Headers @{ 'X-Keyfactor-Requested-With' = 'APIClient' }
if ($outputLog) { Add-Content -Path $outputFile -Value "Got back $($response)" }
if ($outputLog) { Add-Content -Path $outputFile -Value "Done: $(Get-Date -format G)" ; Add-Content -Path $outputFile -Value "" }