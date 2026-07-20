$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$output = Join-Path $root "assets\ui\advancements"
$resolvedRoot = [IO.Path]::GetFullPath($root)
$resolvedOutput = [IO.Path]::GetFullPath($output)
if (-not $resolvedOutput.StartsWith($resolvedRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Advancement icon output resolved outside the project."
}

New-Item -ItemType Directory -Path $resolvedOutput -Force | Out-Null
$icons = [ordered]@{
    "fresh-from-the-globe" = '<circle cx="48" cy="39" r="22"/><path d="M27 39h42M48 17c-9 8-9 36 0 44M48 17c9 8 9 36 0 44"/><circle cx="48" cy="73" r="7" fill="#70d3cf" stroke="none"/>'
    "clean-wrapper" = '<circle cx="43" cy="48" r="25"/><path d="M23 42c13 8 27 9 42 2M26 58c11 6 23 7 35 3" stroke="#70d3cf"/><path d="m68 19 2 7 7 2-7 2-2 7-2-7-7-2 7-2Z"/>'
    "five-star-batch" = '<path d="m48 17 5 11 12 1-9 8 3 12-11-6-11 6 3-12-9-8 12-1Z"/><path d="m24 58 3 7 8 1-6 5 2 8-7-4-7 4 2-8-6-5 8-1Zm48 0 3 7 8 1-6 5 2 8-7-4-7 4 2-8-6-5 8-1Z"/>'
    "speeding-sweet" = '<circle cx="56" cy="48" r="18"/><path d="M12 32h23M8 48h26M15 64h20"/><path d="m53 37 9 11-9 11"/>'
    "terminal-sugar" = '<circle cx="48" cy="48" r="28"/><path d="m53 17-17 29h13l-7 33 20-38H49Z" fill="#70d3cf" stroke="#fff0c7"/>'
    "straight-as-glass" = '<path d="M20 18v60M76 18v60M31 27h34M31 69h34"/><path d="M48 64V30m0 0-9 10m9-10 9 10"/>'
    "perfect-stop" = '<circle cx="48" cy="48" r="29"/><circle cx="48" cy="48" r="16"/><circle cx="48" cy="48" r="5" fill="#70d3cf" stroke="none"/><path d="M48 10v9M48 77v9M10 48h9M77 48h9"/>'
    "blue-streak" = '<path d="m18 27 20 21-20 21M40 27l20 21-20 21M62 27l16 21-16 21"/><path d="M8 16h30M8 80h30" stroke="#70d3cf"/>'
    "double-bounce" = '<circle cx="31" cy="35" r="9"/><circle cx="65" cy="35" r="9"/><path d="M15 73c6-22 27-22 33 0 6-22 27-22 33 0"/><path d="M31 48v9M65 48v9" stroke="#70d3cf"/>'
    "feather-touch" = '<path d="M70 17C43 18 25 37 25 65c18 2 36-8 45-48Z"/><path d="M20 78c13-19 27-31 45-45M34 61l-1-15M48 49l0-14M42 56l15 1M53 43l13 1"/>'
    "against-the-wind" = '<path d="M11 34h44c10 0 10-15 1-15-5 0-8 3-8 7M11 49h62c12 0 12 18 1 18-6 0-9-4-9-8M11 64h33"/><circle cx="24" cy="49" r="8" fill="#70d3cf" stroke="#fff0c7"/>'
    "perfect-switch" = '<path d="M48 81V50M48 50 25 23M48 50l23-27"/><path d="M25 23h15M25 23v15M71 23H56M71 23v15"/><circle cx="48" cy="66" r="7" fill="#70d3cf" stroke="#fff0c7"/>'
    "bullseye" = '<circle cx="48" cy="48" r="31"/><circle cx="48" cy="48" r="18"/><circle cx="48" cy="48" r="6" fill="#70d3cf" stroke="none"/><path d="m70 26 13-13M71 13h12v12"/>'
    "untouchable" = '<path d="M48 14 72 25v19c0 17-11 29-24 38-13-9-24-21-24-38V25Z"/><path d="M15 31 7 23M81 31l8-8M15 61l-8 8M81 61l8 8"/><circle cx="48" cy="45" r="9" fill="#70d3cf" stroke="none"/>'
    "moving-with-it" = '<rect x="18" y="42" width="60" height="18" rx="4"/><circle cx="31" cy="69" r="6"/><circle cx="65" cy="69" r="6"/><path d="M28 31h40M57 21l11 10-11 10" stroke="#70d3cf"/>'
    "piston-perfect" = '<path d="M19 27h25v20H19zM44 33h22v8H44zM66 21h12v32H66z"/><path d="M26 47v25M37 47v25M16 72h34"/><circle cx="72" cy="65" r="10" fill="#70d3cf" stroke="#fff0c7"/>'
    "full-account" = '<path d="M19 62a32 32 0 0 1 58 0"/><path d="M28 57a22 22 0 0 1 40 0"/><path d="m48 58 17-24" stroke="#70d3cf"/><circle cx="48" cy="58" r="6"/><path d="M19 72h58"/>'
    "sugar-breaker" = '<path d="M19 18h58v60H19z"/><path d="m48 18-7 19 11 7-13 14 8 20M41 37l-17 8M52 44l20-9M39 58l-15 8M47 61l18 8" stroke="#70d3cf"/>'
    "vacuum-packed" = '<path d="M77 22C40 12 17 31 20 54c3 22 31 29 46 15 13-12 3-32-12-32-12 0-18 13-11 21 5 6 15 3 15-4"/><path d="m70 15 8 7-10 5"/><circle cx="48" cy="53" r="5" fill="#70d3cf" stroke="none"/>'
}

$header = '<svg xmlns="http://www.w3.org/2000/svg" width="96" height="96" viewBox="0 0 96 96"><rect x="3" y="3" width="90" height="90" rx="18" fill="#22383d" stroke="#70d3cf" stroke-width="4"/><g fill="none" stroke="#fff0c7" stroke-width="5" stroke-linecap="round" stroke-linejoin="round">'
$footer = '</g></svg>'
$utf8 = New-Object Text.UTF8Encoding($false)
foreach ($entry in $icons.GetEnumerator()) {
    $path = Join-Path $resolvedOutput ($entry.Key + ".svg")
    [IO.File]::WriteAllText($path, $header + $entry.Value + $footer, $utf8)
}

Write-Output "GENERATED_ADVANCEMENT_ICONS=$($icons.Count)"
