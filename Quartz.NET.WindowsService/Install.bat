%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\installutil.exe G:\live_zhaorui\ZrryAdmin\ZrryAdmin_api\Quartz.NET.WindowsService\bin\Debug\Quartz.NET.WindowsService.exe

Net Start Quartz.Net

sc config Quartz.Net start= auto