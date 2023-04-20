param (
    [switch]$Build,
    [switch]$Test
)

if ($Build) {
    try {
        Push-Location src
        dotnet publish
    }
    finally {
        Pop-Location
    }
}

if ($Test) {
    "Not implemented"
}
