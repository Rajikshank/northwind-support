#Requires -Version 7.0
param(
    [string]$BaseUrl = 'http://127.0.0.1:5080',
    [string]$OutputPath = 'artifacts/api-results.json'
)
$ErrorActionPreference = 'Stop'
$checks = [System.Collections.Generic.List[object]]::new()
function Check([string]$Name, [bool]$Passed) {
    if (-not $Passed) { throw "Verification failed: $Name" }
    $checks.Add([pscustomobject]@{ check = $Name; result = 'PASS' })
}
function Call([string]$Method, [string]$Path, $Body = $null, $Headers = @{}) {
    $parameters = @{ Method = $Method; Uri = "$BaseUrl$Path"; Headers = $Headers; SkipHttpErrorCheck = $true; TimeoutSec = 30 }
    if ($null -ne $Body) { $parameters.Body = $Body | ConvertTo-Json -Depth 8; $parameters.ContentType = 'application/json' }
    $response = Invoke-WebRequest @parameters
    $data = if ($response.Content -and $response.Headers['Content-Type'] -match 'json') { $response.Content | ConvertFrom-Json } else { $response.Content }
    return [pscustomobject]@{ Status = [int]$response.StatusCode; Data = $data }
}
Check 'anonymous database health' ((Call GET '/health').Status -eq 200)
foreach ($path in @('/api/tickets','/api/tickets/1','/api/customers','/api/agents','/api/categories','/api/tickets/1/escalations','/api/auth/me')) {
    Check "anonymous $path returns 401" ((Call GET $path).Status -eq 401)
}
$fixture = @{ email = 'alex.turner@northwind-support.example'; password = 'LocalDemo-Only!2026' }
$wrong = Call POST '/api/auth/login' @{email=$fixture.email;password='wrong-password'}
$missing = Call POST '/api/auth/login' @{email='unknown@example.com';password='wrong-password'}
$inactive = Call POST '/api/auth/login' @{email='yuki.tanaka@northwind-support.example';password=$fixture.password}
Check 'generic wrong/unknown/inactive login failures' ($wrong.Status -eq 401 -and $missing.Status -eq 401 -and $inactive.Status -eq 401 -and $wrong.Data.detail -eq $missing.Data.detail -and $wrong.Data.detail -eq $inactive.Data.detail)
$login = Call POST '/api/auth/login' $fixture
Check 'seeded login succeeds' ($login.Status -eq 200 -and $login.Data.token.Length -gt 20)
$headers = @{Authorization="Bearer $($login.Data.token)"}
$me = Call GET '/api/auth/me' $null $headers
Check 'authenticated identity' ($me.Status -eq 200 -and $me.Data.email -eq $fixture.email)
$again = Call POST '/api/auth/login' $fixture
Check 'previous successful login survives SQL round trip' ($again.Status -eq 200 -and $null -ne $again.Data.previousSuccessfulLoginAtUtc)
$initial = (Call GET '/api/tickets?pageSize=100' $null $headers).Data
Check 'database has seeded tickets' ($initial.totalCount -ge 40 -and $initial.items.Count -eq $initial.totalCount)
$agents = (Call GET '/api/agents' $null $headers).Data
$categories = (Call GET '/api/categories' $null $headers).Data
$customers = (Call GET '/api/customers' $null $headers).Data
Check 'reference data is seeded' ($agents.Count -eq 6 -and $categories.Count -eq 5 -and $customers.Count -eq 6)
$filters = @(
    @{Query='priority=High'; Expected=@($initial.items | Where-Object priority -eq 'High')},
    @{Query='priority=Low'; Expected=@($initial.items | Where-Object priority -eq 'Low')},
    @{Query='status=InProgress'; Expected=@($initial.items | Where-Object status -eq 'InProgress')},
    @{Query='categoryId=2'; Expected=@($initial.items | Where-Object {$_.category.id -eq 2})},
    @{Query='customerId=1'; Expected=@($initial.items | Where-Object {$_.customer.id -eq 1})},
    @{Query='assignedAgentId=1'; Expected=@($initial.items | Where-Object {$_.assignedAgent.id -eq 1})},
    @{Query='unassignedOnly=true'; Expected=@($initial.items | Where-Object {$null -eq $_.assignedAgent})},
    @{Query='unassignedOnly=true&assignedAgentId=1'; Expected=@()},
    @{Query='priority=High&status=InProgress'; Expected=@($initial.items | Where-Object {$_.priority -eq 'High' -and $_.status -eq 'InProgress'})},
    @{Query='search=%20contoso%20'; Expected=@($initial.items | Where-Object {$_.title -like '*contoso*' -or $_.reference -like '*contoso*' -or $_.customer.name -like '*contoso*'})},
    @{Query='search=TCK-0001'; Expected=@($initial.items | Where-Object reference -eq 'TCK-0001')}
)
foreach ($filter in $filters) {
    $result = (Call GET "/api/tickets?pageSize=100&$($filter.Query)" $null $headers).Data
    $expectedIds = ($filter.Expected.id | Sort-Object) -join ','
    $actualIds = ($result.items.id | Sort-Object) -join ','
    Check "SQL filter/count $($filter.Query)" ($result.totalCount -eq $filter.Expected.Count -and $actualIds -eq $expectedIds)
}
foreach ($state in @('WithinSla','AtRisk','Breached','Met','NotApplicable')) {
    $result = (Call GET "/api/tickets?slaStatus=$state&pageSize=100" $null $headers).Data
    $expected = @($initial.items | Where-Object slaStatus -eq $state)
    Check "SQL SLA filter/count $state" ($result.totalCount -eq $expected.Count -and @($result.items | Where-Object slaStatus -ne $state).Count -eq 0)
}
$page = (Call GET '/api/tickets?priority=High&pageSize=2&page=2' $null $headers).Data
$high = @($initial.items | Where-Object priority -eq 'High')
Check 'filtered pager/count' ($page.totalCount -eq $high.Count -and $page.totalPages -eq [math]::Ceiling($high.Count / 2) -and $page.items.Count -eq 2)
$premium = $customers | Where-Object name -Like '*Adventure Works*' | Select-Object -First 1
$outage = $categories | Where-Object name -eq 'Outage'
$eligible = @($agents | Where-Object {$_.isActive -and $_.openTicketCount -lt $_.maxOpenTickets -and $outage.id -in $_.specializations.id} | Sort-Object openTicketCount,id)
$created = Call POST '/api/tickets' @{title='Live verification outage';description='Disposable assessment verification fixture.';customerId=$premium.id;categoryId=$outage.id;requestedPriority='Medium'} $headers
Check 'outage forces Critical and assigns lowest eligible workload' ($created.Status -eq 201 -and $created.Data.priority -eq 'Critical' -and $created.Data.assignedAgent.id -eq $eligible[0].id)
$hours = ([datetime]$created.Data.dueAtUtc - [datetime]$created.Data.createdAtUtc).TotalHours
Check 'Premium Critical window is 2 hours' ([math]::Abs($hours - 2) -lt 0.000001)
Check 'Critical escalation rejected' ((Call POST "/api/tickets/$($created.Data.id)/escalate" @{reason='Cannot raise Critical further'} $headers).Status -eq 409)
$standard = $customers | Where-Object name -Like '*Fabrikam*' | Select-Object -First 1
$billing = $categories | Where-Object name -eq 'Billing'
$newTicket = Call POST '/api/tickets' @{title='Live verification billing';description='Disposable assessment escalation fixture.';customerId=$standard.id;categoryId=$billing.id;requestedPriority='High'} $headers
Check 'standard High billing window is 8 hours' ($newTicket.Status -eq 201 -and [math]::Abs(([datetime]$newTicket.Data.dueAtUtc - [datetime]$newTicket.Data.createdAtUtc).TotalHours - 8) -lt 0.000001)
$id = $newTicket.Data.id
Check 'invalid reason rejected before escalation' ((Call POST "/api/tickets/$id/escalate" @{reason='bad'} $headers).Status -eq 400)
$escalated = Call POST "/api/tickets/$id/escalate" @{reason='Customer outage requires immediate attention';escalatedBy='spoofed-browser-actor'} $headers
Check 'escalation succeeds and ignores spoofed actor' ($escalated.Status -eq 200 -and $escalated.Data.ticket.priority -eq 'Critical' -and $escalated.Data.escalation.escalatedBy -eq "agent:$($me.Data.id)")
$history = (Call GET "/api/tickets/$id/escalations" $null $headers).Data
Check 'immutable history has before and after priority and deadline' ($history.Count -eq 1 -and $history[0].fromPriority -eq 'High' -and $history[0].toPriority -eq 'Critical' -and $history[0].fromDueAtUtc -eq $newTicket.Data.dueAtUtc -and $history[0].toDueAtUtc -eq $escalated.Data.ticket.dueAtUtc)
Check 'escalation deadline recomputed from escalation time' ([math]::Abs(([datetime]$history[0].toDueAtUtc - [datetime]$history[0].escalatedAtUtc).TotalHours - 4) -lt 0.000001)
Check 'unknown history returns 404' ((Call GET '/api/tickets/999999/escalations' $null $headers).Status -eq 404)
$report = [ordered]@{observedAt=[datetime]::UtcNow.ToString('o');backend='Live API with SQL Server';baseUrl=$BaseUrl;checks=$checks;note='Only fixture data; credentials and JWTs excluded. Creates two verification tickets.'}
$reportDirectory = Split-Path -Parent $OutputPath
if ($reportDirectory) { New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null }
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutputPath -Encoding utf8
Write-Output "PASS: $($checks.Count) live checks. Report: $OutputPath"
