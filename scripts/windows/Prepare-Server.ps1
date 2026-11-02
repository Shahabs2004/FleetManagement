[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$ApplicationPoolName = "FleetManagement",

    [Parameter(Mandatory = $false)]
    [string]$DataDirectory = "$env:ProgramData\FleetManagement"
)

$ErrorActionPreference = "Stop"

# این اسکریپت باید با دسترسی Administrator روی Windows Server اجرا شود.
# پوشه خارج از ریشه وب ایجاد می‌شود تا فایل‌های bootstrap قابل دانلود نباشند.
New-Item -ItemType Directory -Path $DataDirectory -Force | Out-Null

# وراثت ACL حذف و دسترسی فقط به SYSTEM، مدیران محلی و Application Pool داده می‌شود.
$applicationPoolIdentity = "IIS AppPool\$ApplicationPoolName"
& icacls $DataDirectory /inheritance:r | Out-Null
$poolGrant = "{0}:(OI)(CI)(M)" -f $applicationPoolIdentity
& icacls $DataDirectory /grant:r "SYSTEM:(OI)(CI)(F)" "BUILTIN\Administrators:(OI)(CI)(F)" $poolGrant | Out-Null

if ($LASTEXITCODE -ne 0) {
    throw "اعمال مجوزهای NTFS برای پوشه محرمانه ناموفق بود."
}

Write-Host "پوشه محافظت‌شده آماده شد: $DataDirectory"
Write-Host "هویت برنامه: $applicationPoolIdentity"
