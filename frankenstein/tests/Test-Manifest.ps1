param([Parameter(Mandatory=$true)][string[]]$Executables)
$ErrorActionPreference='Stop'
Add-Type @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class ManifestReader {
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr LoadLibraryEx(string path,IntPtr file,uint flags);
 [DllImport("kernel32.dll")] static extern IntPtr FindResource(IntPtr module,IntPtr name,IntPtr type);
 [DllImport("kernel32.dll")] static extern IntPtr LoadResource(IntPtr module,IntPtr resource);
 [DllImport("kernel32.dll")] static extern IntPtr LockResource(IntPtr resource);
 [DllImport("kernel32.dll")] static extern uint SizeofResource(IntPtr module,IntPtr resource);
 [DllImport("kernel32.dll")] static extern bool FreeLibrary(IntPtr module);
 public static string Read(string path){
  IntPtr module=LoadLibraryEx(path,IntPtr.Zero,2);if(module==IntPtr.Zero)throw new Exception("Não foi possível ler o PE.");
  try{IntPtr r=FindResource(module,(IntPtr)1,(IntPtr)24);if(r==IntPtr.Zero)throw new Exception("Manifesto ausente.");byte[] bytes=new byte[SizeofResource(module,r)];Marshal.Copy(LockResource(LoadResource(module,r)),bytes,0,bytes.Length);return Encoding.UTF8.GetString(bytes).Trim('\0','\uFEFF');}
  finally{FreeLibrary(module);}
 }
}
'@
foreach($file in $Executables){
    [xml]$xml=[ManifestReader]::Read([IO.Path]::GetFullPath($file))
    $level=$xml.SelectSingleNode("//*[local-name()='requestedExecutionLevel']")
    if(-not $level -or $level.level -ne 'requireAdministrator' -or $level.uiAccess -ne 'false'){throw "Elevação incorreta no executável: $file"}
    Write-Output ('OK: requireAdministrator embutido em '+[IO.Path]::GetFileName($file))
}
