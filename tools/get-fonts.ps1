# Downloads the WebCherry brand fonts into wwwroot/fonts so the app serves them itself.
#
# Why self-hosted: the Content-Security-Policy only allows fonts from this site
# (font-src 'self'), so Google Fonts links would be blocked - and that's on purpose,
# no third party gets to see who uses the app.
#
# Run once from the repo root:   pwsh tools/get-fonts.ps1
# The files are small (~25-35 KB each) and safe to commit.
# If a download fails, grab the same files from https://fonts.google.com (Space Grotesk, Inter,
# JetBrains Mono), convert/rename to match the names below, and drop them in wwwroot/fonts.

$ErrorActionPreference = 'Stop'
$target = Join-Path $PSScriptRoot '..\PersonalFinanceTracker.Web\wwwroot\fonts'
New-Item -ItemType Directory -Force -Path $target | Out-Null

# Fontsource serves the Google Fonts files as plain .woff2 (latin subset).
$fonts = @(
    @{ Id = 'space-grotesk';  Weights = 500, 700 },
    @{ Id = 'inter';          Weights = 400, 500, 600 },
    @{ Id = 'jetbrains-mono'; Weights = 400, 500 }
)

foreach ($font in $fonts) {
    foreach ($weight in $font.Weights) {
        $name = "$($font.Id)-latin-$weight-normal.woff2"
        $url  = "https://cdn.jsdelivr.net/fontsource/fonts/$($font.Id)@latest/latin-$weight-normal.woff2"
        Write-Host "Downloading $name"
        Invoke-WebRequest -Uri $url -OutFile (Join-Path $target $name)
    }
}

Write-Host "Done. Restart the app and hard-refresh the browser (Ctrl+F5)."
