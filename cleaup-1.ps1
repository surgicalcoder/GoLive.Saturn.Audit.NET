# Set the prefix
$prefix = "GoLive.Saturn."
# Set the target framework
$targetFramework = "net8.0"

# Get all csproj files recursively
$csprojFiles = Get-ChildItem -Path . -Filter *.csproj -Recurse

foreach ($file in $csprojFiles) {
    # Read the content of the csproj file
    $content = Get-Content -Path $file.FullName
    
    # Replace PackageId and AssemblyName with prefixed values
    $content = $content -replace '(<AssemblyName>)(.*?)(<\/AssemblyName>)', ('$1' + $prefix + '$2$3')
    $content = $content -replace '(<PackageId>)(.*?)(<\/PackageId>)', ('$1' + $prefix + '$2$3')
    
    # Replace TargetFrameworks with net8.0
    $content = $content -replace '(<TargetFrameworks>)(.*?)(<\/TargetFrameworks>)', ('$1' + $targetFramework + '$3')
    
    # Write the modified content back to the file
    $content | Set-Content -Path $file.FullName
}

Write-Host "Prefixing and TargetFramework modification complete."