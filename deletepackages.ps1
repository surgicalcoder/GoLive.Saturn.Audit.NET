# Get all *.nupkg files recursively in the current directory and its subdirectories
$nugetPackages = Get-ChildItem -Path . -Filter *.nupkg -File -Recurse

# Loop through each *.nupkg file and delete it
foreach ($package in $nugetPackages) {
    Remove-Item $package.FullName -Force
}

Write-Host "All *.nupkg files have been deleted."
