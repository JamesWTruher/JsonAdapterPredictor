param (
    [switch]$Publish,
    [switch]$Release,
    [switch]$Test
)

$config = $Release.ToBool() ? "Release" : "Debug"
if ( ! $Publish ) {
    wait-debugger
    try {
        Push-Location "$PSScriptRoot/src"
        dotnet build --configuration $config
    }
    finally {
        pop-location
    }
}
else {
    try {
        Push-Location "$PSScriptRoot/src"
        if ( $Release ) {
            dotnet publish --configuration Release
        }
        else {
            dotnet publish --configuration Debug
        }
    }
    finally {
        Pop-Location
    }
}

if ($Test) {
    "Not implemented"
}
