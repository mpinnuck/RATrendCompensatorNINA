param(
	[string]$OutputZip = "trendplugin.zip"
)

$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($root)) {
	$root = Get-Location
}

$sourceRoot = (Resolve-Path $PSScriptRoot).Path
$outputPath = Join-Path $sourceRoot $OutputZip

if (Test-Path $outputPath) {
	Remove-Item $outputPath -Force
}

$excludeDirs = @(
	".git",
	".vs",
	"bin",
	"obj"
)

$files = Get-ChildItem -Path $sourceRoot -Recurse -File | Where-Object {
	$full = $_.FullName

	if ($_.Name -eq $OutputZip) { return $false }

	foreach ($dir in $excludeDirs) {
		if ($full -match "[\\/]$([regex]::Escape($dir))([\\/]|$)") {
			return $false
		}
	}

	return $true
}

if (-not $files) {
	throw "No files found to archive."
}

$relativePaths = $files | ForEach-Object {
	$_.FullName.Substring($sourceRoot.Length).TrimStart('\\')
}

Push-Location $sourceRoot
try {
	Compress-Archive -Path $relativePaths -DestinationPath $outputPath -CompressionLevel Optimal
}
finally {
	Pop-Location
}

Write-Host "Created archive: $outputPath"