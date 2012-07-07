Framework "4.0"

Properties {
	$build_dir = Split-Path $psake.build_script_file
    $build_cfg = "Debug"
}

Task default -Depends Test

Task Deps {
	Exec { & "$build_dir\tools\nuget\nuget.exe" install NUnit.Runners -Version "2.6.0.12051" -ExcludeVersion -OutputDirectory "$build_dir\tools" }
    
    Get-ChildItem "$build_dir\src\*\packages.config" -Exclude .nuget | ForEach-Object {
    	Write-Host "Downloading packages defined in $_"
    	Exec { & "$build_dir\tools\nuget\nuget.exe" install $_ -OutputDirectory "$build_dir\lib" }
    }
}

Task AssemblyInfo {
    $version = Get-Content $build_dir\version.txt
    $year = (Get-Date).Year
    @(
        "[assembly: System.Reflection.AssemblyTitle(`"Log4Rabbit`")]",
        "[assembly: System.Reflection.AssemblyProduct(`"Log4Rabbit`")]",
        "[assembly: System.Reflection.AssemblyDescription(`"RabbitMQ appender for log4net`")]",
        "[assembly: System.Reflection.AssemblyCopyright(`"Copyright � Gian Marco Gherardi $year`")]",
        "[assembly: System.Reflection.AssemblyTrademark(`"`")]",
        "[assembly: System.Reflection.AssemblyCompany(`"Gian Marco Gherardi`")]",
        "[assembly: System.Reflection.AssemblyConfiguration(`"$build_cfg`")]",
        "[assembly: System.Reflection.AssemblyVersion(`"$version`")]",
        "[assembly: System.Reflection.AssemblyFileVersion(`"$version`")]",
        "[assembly: System.Reflection.AssemblyInformationalVersion(`"$version`")]"
    ) | Out-File "$build_dir\src\SharedAssemblyInfo.cs" -Encoding 'utf8'
}

Task Compile -Depends Deps, AssemblyInfo {
    Write-Host "Compiling in $build_cfg configuration"
	Exec { msbuild "$build_dir\src\Log4Rabbit.sln" /t:Build /p:Configuration=$build_cfg /v:quiet /nologo }
}

Task Test -Depends Compile {
    $TestDlls = Get-ChildItem "$build_dir\src\*\bin\$build_cfg\*.Tests.dll"
    Exec { & "$build_dir\tools\NUnit.Runners\tools\nunit-console.exe" /nologo /noresult /framework=4.0.30319 @TestDlls }
}

Task Publish -Depends Clean, Test {
    New-Item build -type directory
   	Exec { & "$build_dir\tools\nuget\nuget.exe" pack "$build_dir\src\Log4Rabbit\Log4Rabbit.csproj" -Build -OutputDirectory $build_dir\build, -Symbols }
    
}

Task Clean {
	# Exec { git --git-dir="$build_dir\.git" --work-tree="$build_dir" clean -d -x -f }
}