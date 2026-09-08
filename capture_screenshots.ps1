Write-Host "Capturing PowerLockGuard screenshots and generating Microsoft Store assets..."
Start-Process (Join-Path $PSScriptRoot "PowerLockGuard.exe") -ArgumentList "--capture-screenshots" -Wait
Write-Host "Screenshots saved to docs\screenshots and StorePackaging\assets successfully!"
