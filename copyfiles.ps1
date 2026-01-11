# Get the current directory
$currentDirectory = Get-Location

# Define the subfolder name
$subfolderName = "_packages"

# Create the subfolder if it doesn't exist
$subfolderPath = Join-Path -Path $currentDirectory -ChildPath $subfolderName
if (-not (Test-Path -Path $subfolderPath -PathType Container)) {
    New-Item -Path $subfolderPath -ItemType Directory | Out-Null
}

# Get all .nupkg files recursively excluding the _packages folder
$nupkgFiles = Get-ChildItem -Path $currentDirectory -Filter "*.nupkg" -File -Recurse | Where-Object { $_.DirectoryName -notlike "*\$subfolderName*" }

# Loop through each .nupkg file and copy it to the _packages subfolder
foreach ($file in $nupkgFiles) {
    $destinationPath = Join-Path -Path $subfolderPath -ChildPath $file.Name
    Copy-Item -Path $file.FullName -Destination $destinationPath -Force
}

Write-Host "Copying .nupkg files completed."