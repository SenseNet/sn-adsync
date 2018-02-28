$srcPath = [System.IO.Path]::GetFullPath(($PSScriptRoot + '\..\..\src'))
$installPackageFolderAD2Portal = "$srcPath\nuget\content-syncad2portal\Admin\tools"
$installPackageFolderPortal2AD = "$srcPath\nuget\content-syncportal2ad\Admin\tools"
$installPackageAD2Portal = "$installPackageFolderAD2Portal\install-syncad2portal.zip"
$installPackagePortal2AD = "$installPackageFolderPortal2AD\install-syncportal2ad.zip"

# delete existing packages
Remove-Item $PSScriptRoot\*.nupkg

New-Item $installPackageFolderAD2Portal -Force -ItemType Directory
New-Item $installPackageFolderPortal2AD -Force -ItemType Directory

Compress-Archive -Path "$srcPath\nuget\snadmin\install-syncad2portal\*" -Force -CompressionLevel Optimal -DestinationPath $installPackageAD2Portal
Compress-Archive -Path "$srcPath\nuget\snadmin\install-syncportal2ad\*" -Force -CompressionLevel Optimal -DestinationPath $installPackagePortal2AD

nuget pack $srcPath\SyncAD2Portal\SyncAD2Portal.nuspec -properties Configuration=Release -OutputDirectory $PSScriptRoot
nuget pack $srcPath\SyncPortal2AD\SyncPortal2AD.nuspec -properties Configuration=Release -OutputDirectory $PSScriptRoot